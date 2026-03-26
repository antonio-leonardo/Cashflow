using Polly;

namespace Cashflow.Gateway;

internal sealed class PollyResilienceHandler(IAsyncPolicy<HttpResponseMessage> policy) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => policy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
}
