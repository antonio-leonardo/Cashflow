using Cashflow.Service.Balance.API.Healthchecks;
using Cashflow.Service.Balance.API.Repositories;
using Cashflow.Shared.Contracts.Api;
using Cashflow.Shared.NoSql.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

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

            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddRedisProviderDependencyInjection(builder.Configuration);
            builder.Services.AddScoped<RedisDailyBalanceRepository>();

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

            app.UseHttpsRedirection();
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

            var endpointGetDailyBalance = app.MapGet(
                "/api/balance/daily/{accountId:guid}",
                async (
                    Guid accountId,
                    DateOnly? date,
                    RedisDailyBalanceRepository repository,
                    CancellationToken cancellationToken) =>
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
                })
                .WithName("GetDailyBalance");

            if (!isLocalEnvironment)
            {
                endpointGetDailyBalance.RequireAuthorization("AuthenticatedUser");
            }

            app.Run();
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
    }
}
