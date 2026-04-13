using Azure.Core;
using Azure.Identity;
using Cashflow.Shared.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Azure.Smoke.Tests
{
    [Trait("Category", "AzureSmoke")]
    public class ApplicationInsightsSmokeTests
    {
        private const string RuntimeRequestOperationName = "cashflow.application-insights.runtime-smoke";
        private const string RuntimeMetricName = "cashflow.application.insights.runtime.smoke";

        [Fact]
        public void Should_Initialize_ApplicationInsights_OpenTelemetry_Pipeline()
        {
            var connectionString = AzureSmokeSettings.GetOptional("APPLICATIONINSIGHTS_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            using var serviceProvider = TelemetrySmokeTestHost.CreateWorkerServiceProvider(
                serviceName: "cashflow-azure-smoke",
                configurationValues: new Dictionary<string, string?>
                {
                    ["Providers:Telemetry"] = "ApplicationInsights",
                    ["ApplicationInsights:ConnectionString"] = connectionString
                });

            var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();
            var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

            Assert.NotNull(tracerProvider);
            Assert.NotNull(meterProvider);
        }

        [Fact]
        public async Task Should_Send_Runtime_Span_And_Metric_To_ApplicationInsights()
        {
            var connectionString = AzureSmokeSettings.GetOptional("APPLICATIONINSIGHTS_CONNECTION_STRING");
            var workspaceId = AzureSmokeSettings.GetOptional("AZURE_SMOKE_LOG_ANALYTICS_WORKSPACE_ID");
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(workspaceId))
            {
                return;
            }

            var runId = Guid.NewGuid().ToString("N");
            var serviceName = $"cashflow-ai-smoke-{runId}";

            using var serviceProvider = TelemetrySmokeTestHost.CreateWorkerServiceProvider(
                serviceName,
                new Dictionary<string, string?>
                {
                    ["Providers:Telemetry"] = "ApplicationInsights",
                    ["ApplicationInsights:ConnectionString"] = connectionString
                });

            TelemetrySmokeTestHost.EmitServerSpan(RuntimeRequestOperationName, runId);
            TelemetrySmokeTestHost.EmitCounterMetric(RuntimeMetricName, runId);
            TelemetrySmokeTestHost.Flush(serviceProvider);

            await WaitForLogAnalyticsQueryAsync(
                workspaceId,
                BuildRequestQuery(serviceName, runId),
                "Application Insights request telemetry");

            await WaitForLogAnalyticsQueryAsync(
                workspaceId,
                BuildMetricQuery(serviceName),
                "Application Insights metric telemetry");
        }

        private static string BuildRequestQuery(string serviceName, string runId) => $$"""
            AppRequests
            | where TimeGenerated > ago(15m)
            | extend Dimensions = todynamic(column_ifexists("Properties", dynamic({})))
            | where AppRoleName == "{{serviceName}}"
            | where Name == "{{RuntimeRequestOperationName}}"
            | where tostring(Dimensions["smoke.run_id"]) == "{{runId}}"
            | top 1 by TimeGenerated desc
            """;

        private static string BuildMetricQuery(string serviceName) => $$"""
            AppMetrics
            | where TimeGenerated > ago(15m)
            | where AppRoleName == "{{serviceName}}"
            | where Name == "{{RuntimeMetricName}}"
            | top 1 by TimeGenerated desc
            """;

        private static async Task WaitForLogAnalyticsQueryAsync(
            string workspaceId,
            string query,
            string description,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            timeout ??= TimeSpan.FromMinutes(3);
            var deadline = DateTimeOffset.UtcNow + timeout.Value;
            Exception? lastError = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    if (await QueryLogAnalyticsHasRowsAsync(workspaceId, query, cancellationToken))
                    {
                        return;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
                {
                    lastError = ex;
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }

            throw new InvalidOperationException(
                $"Timed out waiting for {description} to become queryable in Log Analytics.",
                lastError);
        }

        private static async Task<bool> QueryLogAnalyticsHasRowsAsync(
            string workspaceId,
            string query,
            CancellationToken cancellationToken)
        {
            var credential = new DefaultAzureCredential();
            var token = await GetLogAnalyticsTokenAsync(credential, cancellationToken);

            using var client = new HttpClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.loganalytics.azure.com/v1/workspaces/{workspaceId}/query");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query,
                    timespan = "PT15M"
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Log Analytics query failed with status {(int)response.StatusCode}: {payload}");
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("tables", out var tables) || tables.GetArrayLength() == 0)
            {
                return false;
            }

            foreach (var table in tables.EnumerateArray())
            {
                if (table.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<string> GetLogAnalyticsTokenAsync(
            TokenCredential credential,
            CancellationToken cancellationToken)
        {
            var scopes = new[]
            {
                "https://api.loganalytics.azure.com/.default",
                "https://monitoring.azure.com/.default"
            };

            Exception? lastError = null;
            foreach (var scope in scopes)
            {
                try
                {
                    var token = await credential.GetTokenAsync(
                        new TokenRequestContext(new[] { scope }),
                        cancellationToken);
                    return token.Token;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new InvalidOperationException(
                "Unable to acquire an Azure Monitor Logs access token for Application Insights smoke queries.",
                lastError);
        }
    }
}
