using Cashflow.Shared.Contracts.Configuration;
using Cashflow.Shared.NoSql.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Cashflow.Service.Balance.API.Healthchecks
{
    public sealed class RedisReadinessHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IConfiguration _configuration;

        public RedisReadinessHealthCheck(
            IConnectionMultiplexer redis,
            IConfiguration configuration)
        {
            _redis = redis;
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            CacheProvider cacheProvider;

            try
            {
                cacheProvider = _configuration.GetConfiguredProvider(
                    "Providers:Cache",
                    CacheProvider.Redis,
                    "cache provider");
            }
            catch (InvalidOperationException ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Cache provider selection is unsupported for daily balance queries.",
                    ex);
            }

            try
            {
                var database = _redis.GetDatabase();
                var latency = await database.PingAsync();

                return HealthCheckResult.Healthy(
                    $"{cacheProvider} ping succeeded in {latency.TotalMilliseconds:F0} ms");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    $"{cacheProvider} connection unavailable for daily balance queries.",
                    ex);
            }
        }
    }
}
