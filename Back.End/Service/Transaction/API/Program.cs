using Cashflow.Service.Transaction.API.Healthchecks;
using Cashflow.Service.Transaction.Application.Commands;
using Cashflow.Service.Transaction.Application.Queries;
using Cashflow.Service.Transaction.Domain;
using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace Cashflow.Service.Transaction.API
{
    public class Program
    {
        private const string TestingEnvironmentName = "Testing";
        protected Program() { }

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var isLocalEnvironment = IsLocalEnvironment(builder.Environment);

            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddCashflowOpenTelemetryForWeb(builder.Configuration, "cashflow-transaction-api");
            builder.Services.AddPostgresProviderDependencyInjection(builder.Configuration);
            builder.Services.AddDatabaseInfrastructureDependencyInjection(builder.Configuration);
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser());

                options.AddPolicy("TransactionsWrite", policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .RequireAssertion(context =>
                            HasScope(context.User, "transactions.write") ||
                            HasRole(context.User, "transactions.writer")));
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "transaction-api-global",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 120,
                            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 1),
                            QueueLimit = builder.Configuration.GetValue<int?>("RateLimiting:QueueLimit") ?? 60,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true
                        }));
            });

            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy("transaction-api alive"), tags: new[] { "live" })
                .AddCheck<TransactionReadinessHealthCheck>("transaction", tags: new[] { "ready" })
                .AddCheck<IdempotencyReadinessHealthCheck>("idempotency", tags: new[] { "ready" });

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
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cashflow Transaction API v1");
                });
            }

            app.UseCashflowCorrelationId();
            await InitializeDatabasesAsync(app);

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

            var endpointPostTransactionsV1 = app.MapPost(
                "/api/v1/transactions",
                CreateTransactionAsync)
                .WithName("CreateTransactionV1");

            var endpointGetTransactionsV1 = app.MapGet(
                "/api/v1/transactions/{id:guid}",
                GetTransactionAsync)
                .WithName("GetTransactionV1");

            var endpointPostTransactionsLegacy = app.MapPost(
                "/api/transactions",
                CreateTransactionAsync)
                .WithName("CreateTransaction");

            var endpointGetTransactionsLegacy = app.MapGet(
                "/api/transactions/{id:guid}",
                GetTransactionAsync)
                .WithName("GetTransaction");

            if (!isLocalEnvironment)
            {
                endpointPostTransactionsV1.RequireAuthorization("TransactionsWrite");
                endpointGetTransactionsV1.RequireAuthorization("AuthenticatedUser");
                endpointPostTransactionsLegacy.RequireAuthorization("TransactionsWrite");
                endpointGetTransactionsLegacy.RequireAuthorization("AuthenticatedUser");
            }

            await app.RunAsync();
        }

        private static async Task<IResult> CreateTransactionAsync(
            CreateTransactionRequest request,
            ICreateTransactionHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var correlationId = http.Request.Headers[ObservabilityConstants.CorrelationIdHeaderName].FirstOrDefault()
                ?? http.TraceIdentifier;
            var userId = http.User.FindFirst("sub")?.Value;

            var command = new CreateTransactionCommand(
                TransactionId: Guid.NewGuid(),
                request.AccountId,
                request.Amount,
                request.Currency,
                request.Type,
                correlationId,
                userId);

            await handler.HandleAsync(command, cancellationToken);

            return Results.Created($"/api/v1/transactions/{command.TransactionId}", new { command.TransactionId });
        }

        private static async Task<IResult> GetTransactionAsync(
            Guid id,
            IGetTransactionQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var model = await handler.HandleAsync(new GetTransactionQuery(id), cancellationToken);
            return model is null ? Results.NotFound() : Results.Ok(model);
        }

        private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string expectedScope)
        {
            return user.Claims
                .Where(claim => claim.Type == "scope" || claim.Type == "scp")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Any(scope => string.Equals(scope, expectedScope, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasRole(System.Security.Claims.ClaimsPrincipal user, string expectedRole)
        {
            return user.Claims
                .Where(claim => claim.Type == "role" || claim.Type == "roles")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLocalEnvironment(IHostEnvironment environment)
        {
            return environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironmentName);
        }

        private static void ConfigureAuthentication(WebApplicationBuilder builder)
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = builder.Configuration["Keycloak:Authority"];
                    options.Audience = builder.Configuration["Keycloak:Audience"];
                    options.MapInboundClaims = false;
                    options.RequireHttpsMetadata = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        RoleClaimType = "roles"
                    };
                });
        }

        private static async Task InitializeDatabasesAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(typeof(Program).FullName!);

            var transactionDb = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
            await transactionDb.Database.EnsureCreatedAsync();
            logger.LogInformation("TransactionDbContext initialized");

            var idempotencyDb = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
            await idempotencyDb.Database.EnsureCreatedAsync();
            logger.LogInformation("IdempotencyDbContext initialized");
        }
    }
    record CreateTransactionRequest(Guid AccountId, decimal Amount, string Currency, TransactionType Type);
}
