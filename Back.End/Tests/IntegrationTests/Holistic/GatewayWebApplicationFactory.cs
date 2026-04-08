using Infrastructure.Test;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
            builder.UseEnvironment("Testing");
            builder.UseSetting("Keycloak:Authority", _keycloak.Authority);
            builder.UseSetting(
                "ReverseProxy:Clusters:transaction-cluster:Destinations:transaction-api:Address",
                _transactionDownstream.BaseAddress.ToString());
            builder.UseSetting(
                "ReverseProxy:Clusters:balance-query-cluster:Destinations:balance-query-api:Address",
                _balanceDownstream.BaseAddress.ToString());

            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                context.Response.Headers["X-Auth-Failure"] =
                                    $"{context.Exception.GetType().Name}: {context.Exception.Message}";

                                return Task.CompletedTask;
                            },
                            OnChallenge = context =>
                            {
                                if (!string.IsNullOrWhiteSpace(context.Options.Authority))
                                {
                                    context.Response.Headers["X-Auth-Authority"] = context.Options.Authority;
                                }

                                if (!string.IsNullOrWhiteSpace(context.ErrorDescription))
                                {
                                    context.Response.Headers["X-Auth-Challenge"] = context.ErrorDescription;
                                }

                                return Task.CompletedTask;
                            }
                        };
                    });
            });
        }
    }
}
