using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cashflow.Service.Transaction.API.Healthchecks
{
    public sealed class IdempotencyReadinessHealthCheck : IHealthCheck
    {
        private readonly IdempotencyDbContext _dbContext;

        public IdempotencyReadinessHealthCheck(IdempotencyDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy("Postgres connection is healthy.")
                    : HealthCheckResult.Unhealthy("Postgres connection failed.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Postgres readiness check failed.", ex);
            }
        }
    }
}