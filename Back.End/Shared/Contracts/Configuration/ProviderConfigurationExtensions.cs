using Microsoft.Extensions.Configuration;

namespace Cashflow.Shared.Contracts.Configuration
{
    public static class ProviderConfigurationExtensions
    {
        public static TEnum GetConfiguredProvider<TEnum>(
            this IConfiguration configuration,
            string configurationPath,
            TEnum defaultValue,
            string providerLabel = "provider")
            where TEnum : struct, Enum
        {
            var configuredValue = configuration[configurationPath];

            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                return defaultValue;
            }

            if (Enum.TryParse<TEnum>(configuredValue, ignoreCase: true, out var provider) &&
                Enum.IsDefined(provider))
            {
                return provider;
            }

            throw new InvalidOperationException(
                $"Unsupported {providerLabel} '{configuredValue}' configured at '{configurationPath}'. Supported values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }
    }
}
