using Cashflow.Shared.Identity.EntraId;
using Cashflow.Shared.Identity.Keycloak;
using Cashflow.Shared.Messaging.AzureServiceBus.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Infrastructure.DependencyInjection
{
    public static class InfrastructureServiceCollectionExtensions
    {
        private static readonly string[] SupportedMessagingProviders =
        [
            "RabbitMq",
            "AzureServiceBus"
        ];

        private static readonly string[] SupportedIdentityProviders =
        [
            "Keycloak",
            "EntraId"
        ];

        /// <summary>
        /// Registra o provedor de mensageria conforme "Providers:Messaging" no appsettings.
        /// Valores aceitos: "RabbitMq" (default) | "AzureServiceBus"
        /// </summary>
        public static IServiceCollection AddCashflowMessaging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var provider = ResolveProvider(
                configuration,
                "Providers:Messaging",
                "RabbitMq",
                SupportedMessagingProviders);

            if (string.Equals(provider, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
            {
                var useManagedIdentity = configuration.GetValue<bool>("AzureServiceBus:UseManagedIdentity");
                var requiredKey = useManagedIdentity
                    ? "AzureServiceBus:Namespace"
                    : "AzureServiceBus:ConnectionString";

                EnsureConfigured(configuration, requiredKey);
            }

            return string.Equals(provider, "AzureServiceBus", StringComparison.OrdinalIgnoreCase)
                ? services.AddAzureServiceBusDependencyInjection(configuration)
                : services.AddRabbitMQDependencyInjection(configuration);
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
            var provider = ResolveProvider(
                configuration,
                "Providers:Identity",
                "Keycloak",
                SupportedIdentityProviders);

            if (string.Equals(provider, "EntraId", StringComparison.OrdinalIgnoreCase))
            {
                EnsureConfigured(configuration, "EntraId:TenantId");
                EnsureConfigured(configuration, "EntraId:ClientId");
                EnsureConfigured(configuration, "EntraId:Audience");
            }
            else
            {
                EnsureConfigured(configuration, "Keycloak:Authority");
                EnsureConfigured(configuration, "Keycloak:Audience");
            }

            return string.Equals(provider, "EntraId", StringComparison.OrdinalIgnoreCase)
                ? services.AddEntraIdAuthentication(configuration, requireHttpsMetadata)
                : services.AddKeycloakAuthentication(configuration, requireHttpsMetadata);
        }

        private static string ResolveProvider(
            IConfiguration configuration,
            string configurationPath,
            string defaultValue,
            IReadOnlyCollection<string> supportedProviders)
        {
            var provider = configuration[configurationPath];

            if (string.IsNullOrWhiteSpace(provider))
            {
                return defaultValue;
            }

            if (supportedProviders.Any(supported =>
                    string.Equals(supported, provider, StringComparison.OrdinalIgnoreCase)))
            {
                return provider;
            }

            throw new InvalidOperationException(
                $"Unsupported provider '{provider}' configured at '{configurationPath}'. Supported values: {string.Join(", ", supportedProviders)}.");
        }

        private static void EnsureConfigured(IConfiguration configuration, string configurationPath)
        {
            if (!string.IsNullOrWhiteSpace(configuration[configurationPath]))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Configuration '{configurationPath}' is required for the selected provider.");
        }
    }
}
