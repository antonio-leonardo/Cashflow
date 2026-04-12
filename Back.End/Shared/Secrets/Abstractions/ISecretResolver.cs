namespace Cashflow.Shared.Secrets.Abstractions
{
    public interface ISecretResolver
    {
        ValueTask<string?> GetSecretAsync(
            string secretName,
            CancellationToken cancellationToken = default);
    }
}
