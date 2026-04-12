using Cashflow.Service.Balance.API.Healthchecks;
using Cashflow.Service.Balance.API.Repositories;
using Cashflow.Shared.Contracts.Api;
using Cashflow.Shared.NoSql.Redis;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using Cashflow.Worker.Report;

namespace Cashflow.Service.Balance.API
{
    public class Program
    {
        private const string TestingEnvironmentName = "Testing";
        protected Program() { }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var isLocalEnvironment = IsLocalEnvironment(builder.Environment);

            builder.Services.AddCashflowSecrets(builder.Configuration);

            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddCashflowOpenTelemetryForWeb(builder.Configuration, "cashflow-balance-query-api");
            builder.Services.AddRedisProviderDependencyInjection(builder.Configuration);
            builder.Services.AddCashflowStorage(builder.Configuration);
            builder.Services.AddMongoDBProviderDependencyInjection(builder.Configuration, "cashflow-report");
            builder.Services.AddScoped<IBalanceReadRepository, RedisDailyBalanceRepository>();
            builder.Services.AddScoped<ReportExportService>();

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser());
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "balance-query-api-global",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 150,
                            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 1),
                            QueueLimit = builder.Configuration.GetValue<int?>("RateLimiting:QueueLimit") ?? 75,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true
                        }));
            });

            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy("balance-query-api alive"), tags: new[] { "live" })
                .AddCheck<RedisReadinessHealthCheck>("redis", tags: new[] { "ready" });

            if (!isLocalEnvironment)
            {
                ConfigureAuthentication(builder);
            }

            var app = builder.Build();

            if (isLocalEnvironment)
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.RoutePrefix = "swagger";
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cashflow Balance Query API v1");
                });
            }

            app.UseCashflowCorrelationId();
            app.UseHttpsRedirection();
            if (!isLocalEnvironment)
            {
                app.UseHsts();
            }
            app.UseRateLimiter();

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready") || check.Tags.Contains("live")
            });

            if (!isLocalEnvironment)
            {
                app.UseAuthentication();
                app.UseAuthorization();
            }

            var endpointGetDailyBalanceV1 = app.MapGet(
                    "/api/v1/balance/daily/{accountId:guid}",
                    GetDailyBalanceAsync)
                .WithName("GetDailyBalanceV1");

            var endpointGetDailyBalanceLegacy = app.MapGet(
                    "/api/balance/daily/{accountId:guid}",
                    GetDailyBalanceAsync)
                .WithName("GetDailyBalance");

            var endpointExportDailyReportV1 = app.MapGet(
                    "/api/v1/balance/reports/daily/{accountId:guid}",
                    ExportDailyReportAsync)
                .WithName("ExportDailyReportV1");

            var endpointExportDailyReportLegacy = app.MapGet(
                    "/api/balance/reports/daily/{accountId:guid}",
                    ExportDailyReportAsync)
                .WithName("ExportDailyReport");

            if (!isLocalEnvironment)
            {
                endpointGetDailyBalanceV1.RequireAuthorization("AuthenticatedUser");
                endpointGetDailyBalanceLegacy.RequireAuthorization("AuthenticatedUser");
                endpointExportDailyReportV1.RequireAuthorization("AuthenticatedUser");
                endpointExportDailyReportLegacy.RequireAuthorization("AuthenticatedUser");
            }

            app.Run();
        }

        private static async Task<IResult> GetDailyBalanceAsync(
            Guid accountId,
            DateOnly? date,
            IBalanceReadRepository repository,
            CancellationToken cancellationToken)
        {
            var referenceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var dailyBalance = await repository.GetDailyBalanceAsync(
                accountId,
                referenceDate,
                cancellationToken);

            return dailyBalance is not null
                ? Results.Ok(new GetDailyBalanceResponse(
                    accountId,
                    referenceDate,
                    dailyBalance.TotalCredits,
                    dailyBalance.TotalDebits,
                    dailyBalance.NetBalance))
                : Results.NotFound();
        }

        private static async Task<IResult> ExportDailyReportAsync(
            Guid accountId,
            DateOnly? date,
            int? downloadLinkExpiryMinutes,
            ReportExportService reportExportService,
            CancellationToken cancellationToken)
        {
            var referenceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            TimeSpan? expiry = downloadLinkExpiryMinutes is > 0
                ? TimeSpan.FromMinutes(downloadLinkExpiryMinutes.Value)
                : null;

            var result = await reportExportService.ExportDailyAsync(
                accountId,
                referenceDate,
                expiry,
                cancellationToken);

            return Results.Ok(new GetDailyReportExportResponse(
                accountId,
                referenceDate,
                result.Path,
                result.DownloadUri,
                result.TransactionCount,
                result.GeneratedAt));
        }

        private static bool IsLocalEnvironment(IHostEnvironment environment)
        {
            return environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironmentName);
        }

        private static void ConfigureAuthentication(WebApplicationBuilder builder)
        {
            builder.Services.AddCashflowIdentity(builder.Configuration);
        }
    }
}
