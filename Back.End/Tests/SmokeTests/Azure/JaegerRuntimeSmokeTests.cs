using Cashflow.Shared.Observability;
using System.Text.Json;

namespace Azure.Smoke.Tests
{
    public class JaegerRuntimeSmokeTests
    {
        [Fact]
        [Trait("Category", "OtlpSmoke")]
        public async Task Should_Send_Runtime_Span_To_Jaeger()
        {
            var otlpEndpoint = AzureSmokeSettings.GetOptionalUri("OTLP_SMOKE_ENDPOINT");
            var jaegerQueryEndpoint = AzureSmokeSettings.GetOptionalUri("JAEGER_SMOKE_QUERY_ENDPOINT");
            if (otlpEndpoint is null || jaegerQueryEndpoint is null)
            {
                return;
            }

            var runId = Guid.NewGuid().ToString("N");
            var serviceName = $"cashflow-jaeger-smoke-{runId}";
            var operationName = $"cashflow.jaeger.runtime.{runId}";

            using var serviceProvider = TelemetrySmokeTestHost.CreateWorkerServiceProvider(
                serviceName,
                new Dictionary<string, string?>
                {
                    ["Providers:Telemetry"] = "Jaeger",
                    ["OpenTelemetry:Otlp:Endpoint"] = otlpEndpoint.ToString()
                });

            TelemetrySmokeTestHost.EmitServerSpan(operationName, runId);
            TelemetrySmokeTestHost.Flush(serviceProvider);

            await WaitForJaegerTraceAsync(jaegerQueryEndpoint, serviceName, operationName);
        }

        private static async Task WaitForJaegerTraceAsync(
            Uri jaegerQueryEndpoint,
            string serviceName,
            string operationName,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            timeout ??= TimeSpan.FromMinutes(2);
            var deadline = DateTimeOffset.UtcNow + timeout.Value;

            using var client = new HttpClient();

            while (DateTimeOffset.UtcNow < deadline)
            {
                var requestUri = new Uri(
                    jaegerQueryEndpoint,
                    $"/api/traces?service={Uri.EscapeDataString(serviceName)}&operation={Uri.EscapeDataString(operationName)}&limit=20");

                using var response = await client.GetAsync(requestUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

                if (document.RootElement.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Array &&
                    data.GetArrayLength() > 0)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            throw new InvalidOperationException(
                $"Timed out waiting for Jaeger to expose trace for service '{serviceName}' and operation '{operationName}'.");
        }
    }
}
