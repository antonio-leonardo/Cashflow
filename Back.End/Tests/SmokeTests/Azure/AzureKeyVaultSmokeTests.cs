using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Cashflow.Shared.Secrets.Abstractions;
using Cashflow.Shared.Secrets.AzureKeyVault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.Smoke.Tests
{
    [Trait("Category", "AzureSmoke")]
    public class AzureKeyVaultSmokeTests
    {
        [Fact]
        public async Task Should_Load_Secret_From_Azure_Key_Vault_ConfigurationProvider()
        {
            var vaultUri = AzureSmokeSettings.GetOptionalUri("AZURE_SMOKE_KEYVAULT_URI");
            var configurationKey = AzureSmokeSettings.GetOptional("AZURE_SMOKE_KEYVAULT_CONFIGURATION_KEY");
            var secretName = AzureSmokeSettings.GetOptional("AZURE_SMOKE_KEYVAULT_SECRET_NAME");
            if (vaultUri is null || string.IsNullOrWhiteSpace(configurationKey) || string.IsNullOrWhiteSpace(secretName))
            {
                return;
            }

            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureKeyVault:VaultUri"] = vaultUri.ToString()
            });

            configuration.AddAzureKeyVaultConfiguration();

            var secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
            var expectedSecret = await secretClient.GetSecretAsync(secretName);

            Assert.Equal(expectedSecret.Value.Value, configuration[configurationKey]);
        }

        [Fact]
        public async Task Should_Resolve_Secret_Through_Azure_Key_Vault_SecretResolver()
        {
            var vaultUri = AzureSmokeSettings.GetOptionalUri("AZURE_SMOKE_KEYVAULT_URI");
            var configurationKey = AzureSmokeSettings.GetOptional("AZURE_SMOKE_KEYVAULT_CONFIGURATION_KEY");
            if (vaultUri is null || string.IsNullOrWhiteSpace(configurationKey))
            {
                return;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureKeyVault:VaultUri"] = vaultUri.ToString()
                })
                .Build();

            var services = new ServiceCollection();
            services.AddAzureKeyVaultSecretResolver(configuration);

            await using var serviceProvider = services.BuildServiceProvider();
            var resolver = serviceProvider.GetRequiredService<ISecretResolver>();
            var resolvedSecret = await resolver.GetSecretAsync(configurationKey);

            Assert.False(string.IsNullOrWhiteSpace(resolvedSecret));
        }
    }
}
