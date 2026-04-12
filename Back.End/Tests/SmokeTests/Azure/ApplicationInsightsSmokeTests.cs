using Cashflow.Shared.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Azure.Smoke.Tests
{
    [Trait("Category", "AzureSmoke")]
    public class ApplicationInsightsSmokeTests
    {
        [Fact]
        public void Should_Initialize_ApplicationInsights_OpenTelemetry_Pipeline()
        {
            var connectionString = AzureSmokeSettings.GetOptional("APPLICATIONINSIGHTS_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Providers:Telemetry"] = "ApplicationInsights",
                    ["ApplicationInsights:ConnectionString"] = connectionString
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCashflowOpenTelemetryForWorker(configuration, "cashflow-azure-smoke");

            using var serviceProvider = services.BuildServiceProvider();
            var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();
            var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

            using var activitySource = new ActivitySource(ObservabilityConstants.BusinessActivitySourceName);
            using var activity = activitySource.StartActivity("azure-smoke-activity");
            activity?.SetTag("smoke.test", true);

            using var meter = new Meter(ObservabilityConstants.BusinessMeterName);
            var counter = meter.CreateCounter<long>("cashflow.azure.smoke");
            counter.Add(1);

            tracerProvider.ForceFlush();
            meterProvider.ForceFlush();

            Assert.NotNull(tracerProvider);
            Assert.NotNull(meterProvider);
        }
    }
}
