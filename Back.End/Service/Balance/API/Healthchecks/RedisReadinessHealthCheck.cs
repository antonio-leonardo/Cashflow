using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Cashflow.Service.Balance.API.Healthchecks
{
    public sealed class RedisReadinessHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisReadinessHealthCheck(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var database = _redis.GetDatabase();
                var latency = await database.PingAsync();

                return HealthCheckResult.Healthy(
                    $"Redis ping succeeded in {latency.TotalMilliseconds:F0} ms");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Redis connection unavailable for daily balance queries.",
                    ex);
            }
        }
    }
}
