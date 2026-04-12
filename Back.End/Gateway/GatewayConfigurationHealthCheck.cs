using Cashflow.Shared.Contracts.Configuration;
using Cashflow.Shared.Identity.Abstractions;
using Cashflow.Shared.Secrets.Abstractions;
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
            try
            {
                var identityProvider = _configuration.GetConfiguredProvider(
                    "Providers:Identity",
                    IdentityProvider.Keycloak);
                var secretsProvider = _configuration.GetConfiguredProvider(
                    "Providers:Secrets",
                    SecretsProvider.Local);
                var clusterAddress = _configuration["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"];
                var balanceClusterAddress = _configuration["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"];

                if (!HasValidIdentityProviderConfiguration(identityProvider) ||
                    !HasValidSecretsProviderConfiguration(secretsProvider) ||
                    string.IsNullOrWhiteSpace(clusterAddress) ||
                    string.IsNullOrWhiteSpace(balanceClusterAddress))
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Gateway configuration is incomplete ({identityProvider}/{secretsProvider}/ReverseProxy)."));
                }

                return Task.FromResult(HealthCheckResult.Healthy("Gateway configuration loaded."));
            }
            catch (InvalidOperationException ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Gateway configuration contains an unsupported provider selection.",
                    ex));
            }
        }

        private bool HasValidIdentityProviderConfiguration(IdentityProvider identityProvider)
        {
            return identityProvider switch
            {
                IdentityProvider.EntraId => !string.IsNullOrWhiteSpace(_configuration["EntraId:TenantId"]) &&
                    !string.IsNullOrWhiteSpace(_configuration["EntraId:ClientId"]) &&
                    !string.IsNullOrWhiteSpace(_configuration["EntraId:Audience"]),
                IdentityProvider.Keycloak => !string.IsNullOrWhiteSpace(_configuration["Keycloak:Authority"]) &&
                    !string.IsNullOrWhiteSpace(_configuration["Keycloak:Audience"]),
                _ => false
            };
        }

        private bool HasValidSecretsProviderConfiguration(SecretsProvider secretsProvider)
        {
            return secretsProvider switch
            {
                SecretsProvider.AzureKeyVault => !string.IsNullOrWhiteSpace(_configuration["AzureKeyVault:VaultUri"]),
                SecretsProvider.Local => true,
                _ => false
            };
        }
    }
}
