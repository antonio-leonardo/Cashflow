using Cashflow.Shared.Contracts.Configuration;
using Cashflow.Shared.Identity.Abstractions;
using Cashflow.Shared.Identity.EntraId;
using Cashflow.Shared.Identity.Keycloak;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.AzureServiceBus.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.Secrets.Abstractions;
using Cashflow.Shared.Secrets.AzureKeyVault;
using Cashflow.Shared.Storage.Abstractions;
using Cashflow.Shared.Storage.AzureBlob;
using Cashflow.Shared.Storage.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Infrastructure.DependencyInjection
{
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddCashflowSecrets(
            this IServiceCollection services,
            ConfigurationManager configuration)
        {
            var provider = configuration.GetConfiguredProvider(
                "Providers:Secrets",
                SecretsProvider.Local);

            return provider switch
            {
                SecretsProvider.AzureKeyVault => AddAzureKeyVaultSecrets(services, configuration),
                SecretsProvider.Local => services.AddSingleton<ISecretResolver, LocalConfigurationSecretResolver>(),
                _ => throw new InvalidOperationException($"Unsupported provider '{provider}' configured at 'Providers:Secrets'.")
            };
        }

        /// <summary>
        /// Registra o provedor de mensageria conforme "Providers:Messaging" no appsettings.
        /// Valores aceitos: "RabbitMq" (default) | "AzureServiceBus"
        /// </summary>
        public static IServiceCollection AddCashflowMessaging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var provider = configuration.GetConfiguredProvider(
                "Providers:Messaging",
                MessagingProvider.RabbitMq);

            EnsureMessagingConfiguration(configuration, provider);

            return provider switch
            {
                MessagingProvider.AzureServiceBus => services.AddAzureServiceBusDependencyInjection(configuration),
                MessagingProvider.RabbitMq => services.AddRabbitMQDependencyInjection(configuration),
                _ => throw new InvalidOperationException($"Unsupported provider '{provider}' configured at 'Providers:Messaging'.")
            };
        }

        /// <summary>
        /// Registra o provedor de identidade/autenticação conforme "Providers:Identity" no appsettings.
        /// Valores aceitos: "Keycloak" (default) | "EntraId"
        /// </summary>
        public static IServiceCollection AddCashflowIdentity(
            this IServiceCollection services,
            IConfiguration configuration,
            bool requireHttpsMetadata = true)
        {
            var provider = configuration.GetConfiguredProvider(
                "Providers:Identity",
                IdentityProvider.Keycloak);

            EnsureIdentityConfiguration(configuration, provider);

            return provider switch
            {
                IdentityProvider.EntraId => services.AddEntraIdAuthentication(configuration, requireHttpsMetadata),
                IdentityProvider.Keycloak => services.AddKeycloakAuthentication(configuration, requireHttpsMetadata),
                _ => throw new InvalidOperationException($"Unsupported provider '{provider}' configured at 'Providers:Identity'.")
            };
        }

        /// <summary>
        /// Registra o provedor de armazenamento de artefatos conforme "Providers:Storage" no appsettings.
        /// Valores aceitos: "Local" (default) | "AzureBlob"
        /// </summary>
        public static IServiceCollection AddCashflowStorage(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var provider = configuration.GetConfiguredProvider(
                "Providers:Storage",
                StorageProvider.Local,
                "storage provider");

            return provider switch
            {
                StorageProvider.AzureBlob => AddAzureBlobStorage(services, configuration),
                StorageProvider.Local => services.AddLocalReportArtifactStore(opts =>
                {
                    var basePath = configuration["LocalStorage:BasePath"];
                    if (!string.IsNullOrWhiteSpace(basePath))
                    {
                        opts.BasePath = basePath;
                    }
                }),
                _ => throw new InvalidOperationException(
                    $"Unsupported storage provider '{provider}' configured at 'Providers:Storage'.")
            };
        }

        private static IServiceCollection AddAzureBlobStorage(
            IServiceCollection services,
            IConfiguration configuration)
        {
            EnsureConfigured(configuration,
                configuration.GetValue<bool>("AzureBlob:UseManagedIdentity")
                    ? "AzureBlob:AccountName"
                    : "AzureBlob:ConnectionString");

            return services.AddAzureBlobReportArtifactStore(configuration);
        }

        internal static void EnsureConfigured(IConfiguration configuration, string configurationPath)
        {
            if (!string.IsNullOrWhiteSpace(configuration[configurationPath]))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Configuration '{configurationPath}' is required for the selected provider.");
        }

        private static IServiceCollection AddAzureKeyVaultSecrets(
            IServiceCollection services,
            ConfigurationManager configuration)
        {
            EnsureConfigured(configuration, "AzureKeyVault:VaultUri");
            configuration.AddAzureKeyVaultConfiguration();
            return services.AddAzureKeyVaultSecretResolver(configuration);
        }

        private static void EnsureMessagingConfiguration(
            IConfiguration configuration,
            MessagingProvider provider)
        {
            switch (provider)
            {
                case MessagingProvider.AzureServiceBus:
                    var useManagedIdentity = configuration.GetValue<bool>("AzureServiceBus:UseManagedIdentity");
                    EnsureConfigured(
                        configuration,
                        useManagedIdentity
                            ? "AzureServiceBus:Namespace"
                            : "AzureServiceBus:ConnectionString");
                    break;

                case MessagingProvider.RabbitMq:
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported provider '{provider}' configured at 'Providers:Messaging'.");
            }
        }

        private static void EnsureIdentityConfiguration(
            IConfiguration configuration,
            IdentityProvider provider)
        {
            switch (provider)
            {
                case IdentityProvider.EntraId:
                    EnsureConfigured(configuration, "EntraId:TenantId");
                    EnsureConfigured(configuration, "EntraId:ClientId");
                    EnsureConfigured(configuration, "EntraId:Audience");
                    break;

                case IdentityProvider.Keycloak:
                    EnsureConfigured(configuration, "Keycloak:Authority");
                    EnsureConfigured(configuration, "Keycloak:Audience");
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported provider '{provider}' configured at 'Providers:Identity'.");
            }
        }
    }
}
