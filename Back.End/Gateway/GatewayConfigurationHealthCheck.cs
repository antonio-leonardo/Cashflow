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
            var identityProvider = _configuration["Providers:Identity"] ?? "Keycloak";
            var clusterAddress = _configuration["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"];
            var balanceClusterAddress = _configuration["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"];

            if (!HasValidIdentityProviderConfiguration(identityProvider) ||
                string.IsNullOrWhiteSpace(clusterAddress) ||
                string.IsNullOrWhiteSpace(balanceClusterAddress))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Gateway configuration is incomplete ({identityProvider}/ReverseProxy)."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Gateway configuration loaded."));
        }

        private bool HasValidIdentityProviderConfiguration(string identityProvider)
        {
            if (string.Equals(identityProvider, "EntraId", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(_configuration["EntraId:TenantId"]) &&
                    !string.IsNullOrWhiteSpace(_configuration["EntraId:ClientId"]) &&
                    !string.IsNullOrWhiteSpace(_configuration["EntraId:Audience"]);
            }

            return !string.IsNullOrWhiteSpace(_configuration["Keycloak:Authority"]) &&
                !string.IsNullOrWhiteSpace(_configuration["Keycloak:Audience"]);
        }
    }
}
