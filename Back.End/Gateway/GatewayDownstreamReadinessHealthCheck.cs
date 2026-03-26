using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace Cashflow.Gateway
{
    public sealed class GatewayDownstreamReadinessHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public GatewayDownstreamReadinessHealthCheck(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var transactionProbe = await ProbeReadyAsync(
                GatewayDownstreamClients.TransactionReadinessClient,
                "transaction-api",
                cancellationToken);

            var balanceProbe = await ProbeReadyAsync(
                GatewayDownstreamClients.BalanceReadinessClient,
                "balance-query-api",
                cancellationToken);

            if (transactionProbe.IsHealthy && balanceProbe.IsHealthy)
            {
                return HealthCheckResult.Healthy(
                    "Gateway reached downstream readiness endpoints (transaction + balance).");
            }

            var data = new Dictionary<string, object>
            {
                ["transaction.status"] = (int)transactionProbe.StatusCode,
                ["balance.status"] = (int)balanceProbe.StatusCode,
                ["transaction.error"] = transactionProbe.Error ?? string.Empty,
                ["balance.error"] = balanceProbe.Error ?? string.Empty
            };

            return HealthCheckResult.Unhealthy(
                $"Downstream readiness check failed (transaction={transactionProbe.StatusCode}, balance={balanceProbe.StatusCode}).",
                data: data);
        }

        private async Task<DownstreamProbeResult> ProbeReadyAsync(
            string clientName,
            string downstreamName,
            CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(clientName);
                using var response = await client.GetAsync(GatewayDownstreamClients.ReadinessPath, cancellationToken);

                return new DownstreamProbeResult(
                    IsHealthy: response.IsSuccessStatusCode,
                    StatusCode: response.StatusCode,
                    Error: response.IsSuccessStatusCode
                        ? null
                        : $"{downstreamName} returned {(int)response.StatusCode}.");
            }
            catch (Exception ex)
            {
                return new DownstreamProbeResult(
                    IsHealthy: false,
                    StatusCode: HttpStatusCode.ServiceUnavailable,
                    Error: $"{downstreamName} probe failed: {ex.Message}");
            }
        }

        private sealed record DownstreamProbeResult(
            bool IsHealthy,
            HttpStatusCode StatusCode,
            string? Error);
    }
}
