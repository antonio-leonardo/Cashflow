using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cashflow.Gateway
{
    public sealed class GatewayConfigurationHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;

        public GatewayConfigurationHealthCheck(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var authority = _configuration["Keycloak:Authority"];
            var audience = _configuration["Keycloak:Audience"];
            var clusterAddress = _configuration["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"];
            var balanceClusterAddress = _configuration["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"];

            if (string.IsNullOrWhiteSpace(authority) ||
                string.IsNullOrWhiteSpace(audience) ||
                string.IsNullOrWhiteSpace(clusterAddress) ||
                string.IsNullOrWhiteSpace(balanceClusterAddress))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Gateway configuration is incomplete (Keycloak/ReverseProxy)."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Gateway configuration loaded."));
        }
    }
}
