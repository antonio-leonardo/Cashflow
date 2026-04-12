using Azure.Core;
using Azure.Identity;
using Cashflow.Shared.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Azure.Smoke.Tests
{
    [Trait("Category", "AzureSmoke")]
    public class EntraIdSmokeTests
    {
        [Fact]
        public async Task Should_Validate_Access_Token_With_EntraId_Configuration()
        {
            var tenantId = AzureSmokeSettings.GetOptional("AZURE_SMOKE_ENTRA_TENANT_ID");
            var clientId = AzureSmokeSettings.GetOptional("AZURE_SMOKE_ENTRA_CLIENT_ID");
            var audience = AzureSmokeSettings.GetOptional("AZURE_SMOKE_ENTRA_AUDIENCE");
            if (string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(audience))
            {
                return;
            }

            var scope = Environment.GetEnvironmentVariable("AZURE_SMOKE_ENTRA_SCOPE") ?? $"{audience}/.default";

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Providers:Identity"] = "EntraId",
                    ["EntraId:TenantId"] = tenantId,
                    ["EntraId:ClientId"] = clientId,
                    ["EntraId:Audience"] = audience
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCashflowIdentity(configuration);

            await using var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            var options = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
            var openIdConfiguration = await GetOpenIdConfigurationAsync(options);

            var credential = new DefaultAzureCredential();
            var accessToken = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { scope }),
                CancellationToken.None);

            var validationParameters = options.TokenValidationParameters.Clone();
            validationParameters.ValidAudience = audience;
            validationParameters.ValidAudiences = new[] { audience };
            validationParameters.IssuerSigningKeys = openIdConfiguration.SigningKeys;
            validationParameters.ValidIssuers = new[] { openIdConfiguration.Issuer };
            validationParameters.ValidateIssuer = true;
            validationParameters.ValidateAudience = true;
            validationParameters.ValidateIssuerSigningKey = true;
            validationParameters.ValidateLifetime = true;

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(accessToken.Token, validationParameters, out var validatedToken);

            Assert.NotNull(principal.Identity);
            Assert.True(principal.Identity.IsAuthenticated);
            Assert.NotNull(validatedToken);
        }

        private static async Task<OpenIdConnectConfiguration> GetOpenIdConfigurationAsync(JwtBearerOptions options)
        {
            if (options.ConfigurationManager is null)
            {
                throw new InvalidOperationException("JwtBearerOptions.ConfigurationManager was not configured for EntraId.");
            }

            var configuration = await options.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);
            if (configuration is OpenIdConnectConfiguration openIdConfiguration)
            {
                return openIdConfiguration;
            }

            throw new InvalidOperationException("Unable to resolve OpenID Connect metadata for EntraId.");
        }
    }
}
