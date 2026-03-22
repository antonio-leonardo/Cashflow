using Infrastructure.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Holistic.Integration.Tests
{
    public sealed class GatewayWebApplicationFactory : WebApplicationFactory<Cashflow.Gateway.Program>
    {
        private readonly KeycloakContainerFixture _keycloak;
        private readonly TransactionApiContainerFixture _downstream;

        public GatewayWebApplicationFactory(
            KeycloakContainerFixture keycloak,
            TransactionApiContainerFixture downstream)
        {
            _keycloak = keycloak;
            _downstream = downstream;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Keycloak:Authority"] = _keycloak.Authority,
                    ["Keycloak:Audience"] = "cashflow-api",
                    ["ReverseProxy:Routes:transaction-route:ClusterId"] = "transaction-cluster",
                    ["ReverseProxy:Routes:transaction-route:Match:Path"] = "/api/transactions/{**catch-all}",
                    ["ReverseProxy:Routes:transaction-route:AuthorizationPolicy"] = "AuthenticatedUser",
                    ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] =
                    _downstream.BaseAddress.ToString().TrimEnd('/')
                };

                config.AddInMemoryCollection(settings);
            });
        }
    }
}