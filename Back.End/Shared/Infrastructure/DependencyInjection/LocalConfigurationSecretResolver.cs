using Cashflow.Shared.Secrets.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Cashflow.Shared.Infrastructure.DependencyInjection
{
    internal sealed class LocalConfigurationSecretResolver : ISecretResolver
    {
        private readonly IConfiguration _configuration;

        public LocalConfigurationSecretResolver(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ValueTask<string?> GetSecretAsync(
            string secretName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
            return ValueTask.FromResult(_configuration[secretName]);
        }
    }
}
