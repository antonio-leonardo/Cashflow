using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.Identity.Abstractions;
using Cashflow.Shared.Observability;
using Cashflow.Shared.Resilience;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

namespace Cashflow.Gateway
{
    public class Program
    {
        protected Program() { }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var isLocalEnvironment = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");

            builder.Services.AddCashflowSecrets(builder.Configuration);

            builder.Services
                .AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
                .AddTransforms(transformBuilderContext =>
                {
                    transformBuilderContext.AddRequestTransform(transformContext =>
                    {
                        var correlationId = transformContext.HttpContext.Request
                            .Headers[ObservabilityConstants.CorrelationIdHeaderName]
                            .ToString();

                        if (!string.IsNullOrWhiteSpace(correlationId))
                        {
                            transformContext.ProxyRequest.Headers.Remove(ObservabilityConstants.CorrelationIdHeaderName);
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                                ObservabilityConstants.CorrelationIdHeaderName,
                                correlationId);
                        }

                        return ValueTask.CompletedTask;
                    });
                });

            builder.Services.AddCashflowOpenTelemetryForWeb(builder.Configuration, "cashflow-gateway");

            builder.Services.AddCashflowIdentity(
                builder.Configuration,
                requireHttpsMetadata: !isLocalEnvironment);

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser());

                options.AddPolicy("TransactionsWrite", policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .RequireAssertion(context =>
                            context.User.HasScope("transactions.write") ||
                            context.User.HasRole("transactions.writer")));
            });

            var downstreamReadyTimeoutSeconds =
                builder.Configuration.GetValue<int?>("DownstreamHealthChecks:TimeoutSeconds") ?? 2;

            var transactionReadinessResilienceOptions = BindHttpResilienceOptions(
                builder.Configuration,
                "Resilience:Downstreams:TransactionReadiness",
                new HttpResiliencePolicyOptions
                {
                    RetryCount = 2,
                    RetryBaseDelayMilliseconds = 200,
                    CircuitBreakerFailureThreshold = 5,
                    CircuitBreakerBreakSeconds = 15,
                    BulkheadParallelization = 20,
                    BulkheadQueue = 60,
                    TimeoutSeconds = 3,
                    EnableFallback = false
                });

            var balanceReadinessResilienceOptions = BindHttpResilienceOptions(
                builder.Configuration,
                "Resilience:Downstreams:BalanceReadiness",
                new HttpResiliencePolicyOptions
                {
                    RetryCount = 2,
                    RetryBaseDelayMilliseconds = 200,
                    CircuitBreakerFailureThreshold = 5,
                    CircuitBreakerBreakSeconds = 15,
                    BulkheadParallelization = 20,
                    BulkheadQueue = 60,
                    TimeoutSeconds = 3,
                    EnableFallback = false
                });

            builder.Services.AddHttpClient(
                GatewayDownstreamClients.TransactionReadinessClient,
                client =>
                {
                    ConfigureDownstreamHealthClient(
                        client,
                        builder.Configuration["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"],
                        downstreamReadyTimeoutSeconds);
                })
                .AddHttpMessageHandler(sp =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Gateway.Resilience.TransactionReadiness");
                    var policy = ResiliencePolicies.GetHttpResiliencePolicy(
                        transactionReadinessResilienceOptions,
                        logger,
                        "gateway-transaction-readiness");
                    return new PollyResilienceHandler(policy);
                });

            builder.Services.AddHttpClient(
                GatewayDownstreamClients.BalanceReadinessClient,
                client =>
                {
                    ConfigureDownstreamHealthClient(
                        client,
                        builder.Configuration["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"],
                        downstreamReadyTimeoutSeconds);
                })
                .AddHttpMessageHandler(sp =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Gateway.Resilience.BalanceReadiness");
                    var policy = ResiliencePolicies.GetHttpResiliencePolicy(
                        balanceReadinessResilienceOptions,
                        logger,
                        "gateway-balance-readiness");
                    return new PollyResilienceHandler(policy);
                });

            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy("gateway alive"), tags: new[] { "live" })
                .AddCheck<GatewayConfigurationHealthCheck>("config", tags: new[] { "ready" })
                .AddCheck<GatewayDownstreamReadinessHealthCheck>("downstreams", tags: new[] { "ready" });

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                var globalPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Global:PermitLimit")
                    ?? builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit")
                    ?? 100;
                var globalWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Global:WindowSeconds")
                    ?? builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds")
                    ?? 1;
                var globalQueueLimit = builder.Configuration.GetValue<int?>("RateLimiting:Global:QueueLimit")
                    ?? builder.Configuration.GetValue<int?>("RateLimiting:QueueLimit")
                    ?? 50;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "gateway-global",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = globalPermitLimit,
                            Window = TimeSpan.FromSeconds(globalWindowSeconds),
                            QueueLimit = globalQueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true
                        }));

                options.AddFixedWindowLimiter(
                    GatewayRateLimiterPolicies.TransactionWrite,
                    policyOptions =>
                    {
                        ConfigureFixedWindowPolicy(
                            policyOptions,
                            builder.Configuration,
                            "RateLimiting:Policies:TransactionWrite",
                            defaultPermitLimit: 40,
                            defaultWindowSeconds: 1,
                            defaultQueueLimit: 20);
                    });

                options.AddFixedWindowLimiter(
                    GatewayRateLimiterPolicies.TransactionRead,
                    policyOptions =>
                    {
                        ConfigureFixedWindowPolicy(
                            policyOptions,
                            builder.Configuration,
                            "RateLimiting:Policies:TransactionRead",
                            defaultPermitLimit: 120,
                            defaultWindowSeconds: 1,
                            defaultQueueLimit: 40);
                    });

                options.AddFixedWindowLimiter(
                    GatewayRateLimiterPolicies.BalanceRead,
                    policyOptions =>
                    {
                        ConfigureFixedWindowPolicy(
                            policyOptions,
                            builder.Configuration,
                            "RateLimiting:Policies:BalanceRead",
                            defaultPermitLimit: 150,
                            defaultWindowSeconds: 1,
                            defaultQueueLimit: 60);
                    });
            });

            var app = builder.Build();

            if (!isLocalEnvironment)
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCashflowCorrelationId();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready") || check.Tags.Contains("live")
            });

            app.MapReverseProxy();

            app.Run();
        }

        private static void ConfigureDownstreamHealthClient(
            HttpClient client,
            string? baseAddress,
            int timeoutSeconds)
        {
            if (Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
            {
                client.BaseAddress = uri;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1));
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                ObservabilityConstants.CorrelationIdHeaderName,
                $"gateway-ready-{Guid.NewGuid():N}");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

        private static void ConfigureFixedWindowPolicy(
            FixedWindowRateLimiterOptions policyOptions,
            IConfiguration configuration,
            string sectionPath,
            int defaultPermitLimit,
            int defaultWindowSeconds,
            int defaultQueueLimit)
        {
            policyOptions.PermitLimit = configuration.GetValue<int?>($"{sectionPath}:PermitLimit") ?? defaultPermitLimit;
            policyOptions.Window = TimeSpan.FromSeconds(
                configuration.GetValue<int?>($"{sectionPath}:WindowSeconds") ?? defaultWindowSeconds);
            policyOptions.QueueLimit = configuration.GetValue<int?>($"{sectionPath}:QueueLimit") ?? defaultQueueLimit;
            policyOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            policyOptions.AutoReplenishment = true;
        }

        private static HttpResiliencePolicyOptions BindHttpResilienceOptions(
            IConfiguration configuration,
            string sectionPath,
            HttpResiliencePolicyOptions defaults)
        {
            configuration.GetSection(sectionPath).Bind(defaults);
            return defaults;
        }
    }

    internal static class GatewayDownstreamClients
    {
        public const string TransactionReadinessClient = "transaction-api-readiness-client";
        public const string BalanceReadinessClient = "balance-query-api-readiness-client";
        public const string ReadinessPath = "/health/ready";
    }

    internal static class GatewayRateLimiterPolicies
    {
        public const string TransactionWrite = "gateway-transaction-write";
        public const string TransactionRead = "gateway-transaction-read";
        public const string BalanceRead = "gateway-balance-read";
    }
}
