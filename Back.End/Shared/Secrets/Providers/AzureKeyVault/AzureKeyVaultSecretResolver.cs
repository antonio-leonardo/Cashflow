using Azure;
using Azure.Security.KeyVault.Secrets;
using Cashflow.Shared.Secrets.Abstractions;

namespace Cashflow.Shared.Secrets.AzureKeyVault
{
    public sealed class AzureKeyVaultSecretResolver : ISecretResolver
    {
        private readonly SecretClient _secretClient;

        public AzureKeyVaultSecretResolver(SecretClient secretClient)
        {
            _secretClient = secretClient;
        }

        public async ValueTask<string?> GetSecretAsync(
            string secretName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            try
            {
                var response = await _secretClient.GetSecretAsync(
                    AzureKeyVaultSecretNameFormatter.Format(secretName),
                    cancellationToken: cancellationToken);

                return response.Value.Value;
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                return null;
            }
        }
    }
}
