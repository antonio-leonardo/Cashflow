namespace Cashflow.Shared.Secrets.AzureKeyVault
{
    internal static class AzureKeyVaultSecretNameFormatter
    {
        public static string Format(string configurationKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);

            return configurationKey
                .Replace("__", "--", StringComparison.Ordinal)
                .Replace(":", "--", StringComparison.Ordinal);
        }
    }
}
