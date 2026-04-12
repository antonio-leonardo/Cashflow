using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Cashflow.Shared.Secrets.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Secrets.AzureKeyVault
{
    public static class ServiceCollectionExtensions
    {
        public static IConfigurationManager AddAzureKeyVaultConfiguration(
            this IConfigurationManager configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var vaultUri = configuration["AzureKeyVault:VaultUri"];
            ArgumentException.ThrowIfNullOrWhiteSpace(vaultUri, "AzureKeyVault:VaultUri");

            configuration.AddAzureKeyVault(
                new Uri(vaultUri),
                CreateCredential(configuration));

            return configuration;
        }

        public static IServiceCollection AddAzureKeyVaultSecretResolver(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var vaultUri = configuration["AzureKeyVault:VaultUri"];
            ArgumentException.ThrowIfNullOrWhiteSpace(vaultUri, "AzureKeyVault:VaultUri");

            services.AddSingleton(_ => CreateCredential(configuration));
            services.AddSingleton(sp =>
                new SecretClient(new Uri(vaultUri), sp.GetRequiredService<DefaultAzureCredential>()));
            services.AddSingleton<ISecretResolver, AzureKeyVaultSecretResolver>();

            return services;
        }

        private static DefaultAzureCredential CreateCredential(IConfiguration configuration)
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = configuration["AzureKeyVault:ManagedIdentityClientId"]
            });
        }
    }
}
