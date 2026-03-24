using Infrastructure.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Holistic.Integration.Tests
{
    public sealed class GatewayWebApplicationFactory : WebApplicationFactory<Cashflow.Gateway.Program>
    {
        private readonly KeycloakContainerFixture _keycloak;
        private readonly TransactionApiContainerFixture _transactionDownstream;
        private readonly BalanceQueryApiContainerFixture _balanceDownstream;

        public GatewayWebApplicationFactory(
            KeycloakContainerFixture keycloak,
            TransactionApiContainerFixture transactionDownstream,
            BalanceQueryApiContainerFixture balanceDownstream)
        {
            _keycloak = keycloak;
            _transactionDownstream = transactionDownstream;
            _balanceDownstream = balanceDownstream;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Keycloak:Authority"] = _keycloak.Authority,
                    ["Keycloak:Audience"] = "cashflow-api",
                    ["ReverseProxy:Routes:transaction-write-route:ClusterId"] = "transaction-cluster",
                    ["ReverseProxy:Routes:transaction-write-route:Match:Path"] = "/api/transactions/{**catch-all}",
                    ["ReverseProxy:Routes:transaction-write-route:Match:Methods:0"] = "POST",
                    ["ReverseProxy:Routes:transaction-write-route:Match:Methods:1"] = "PUT",
                    ["ReverseProxy:Routes:transaction-write-route:Match:Methods:2"] = "PATCH",
                    ["ReverseProxy:Routes:transaction-write-route:Match:Methods:3"] = "DELETE",
                    ["ReverseProxy:Routes:transaction-write-route:AuthorizationPolicy"] = "TransactionsWrite",
                    ["ReverseProxy:Routes:transaction-read-route:ClusterId"] = "transaction-cluster",
                    ["ReverseProxy:Routes:transaction-read-route:Match:Path"] = "/api/transactions/{**catch-all}",
                    ["ReverseProxy:Routes:transaction-read-route:Match:Methods:0"] = "GET",
                    ["ReverseProxy:Routes:transaction-read-route:AuthorizationPolicy"] = "AuthenticatedUser",
                    ["ReverseProxy:Routes:balance-daily-read-route:ClusterId"] = "balance-query-cluster",
                    ["ReverseProxy:Routes:balance-daily-read-route:Match:Path"] = "/api/balance/{**catch-all}",
                    ["ReverseProxy:Routes:balance-daily-read-route:Match:Methods:0"] = "GET",
                    ["ReverseProxy:Routes:balance-daily-read-route:AuthorizationPolicy"] = "AuthenticatedUser",
                    ["ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address"] =
                    _transactionDownstream.BaseAddress.ToString().TrimEnd('/'),
                    ["ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address"] =
                    _balanceDownstream.BaseAddress.ToString().TrimEnd('/')
                };

                config.AddInMemoryCollection(settings);
            });
        }
    }
}
