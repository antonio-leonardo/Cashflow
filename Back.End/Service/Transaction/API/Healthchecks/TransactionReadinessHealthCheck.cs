using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cashflow.Service.Transaction.API.Healthchecks
{
    public sealed class TransactionReadinessHealthCheck : IHealthCheck
    {
        private readonly TransactionDbContext _dbContext;

        public TransactionReadinessHealthCheck(TransactionDbContext dbContext)
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
                    ? HealthCheckResult.Healthy("Transaction database connection is healthy.")
                    : HealthCheckResult.Unhealthy("Transaction database connection failed.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Transaction database readiness check failed.", ex);
            }
        }
    }
}