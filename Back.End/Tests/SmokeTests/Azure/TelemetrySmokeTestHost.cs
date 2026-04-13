using Cashflow.Shared.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Azure.Smoke.Tests
{
    internal static class TelemetrySmokeTestHost
    {
        public static ServiceProvider CreateWorkerServiceProvider(
            string serviceName,
            IDictionary<string, string?> configurationValues)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCashflowOpenTelemetryForWorker(configuration, serviceName);
            return services.BuildServiceProvider();
        }

        public static void EmitServerSpan(string operationName, string runId)
        {
            using var activitySource = new ActivitySource(ObservabilityConstants.BusinessActivitySourceName);
            using var activity = activitySource.StartActivity(operationName, ActivityKind.Server);

            activity?.SetTag("smoke.test", true);
            activity?.SetTag("smoke.run_id", runId);
            activity?.SetTag("http.request.method", "GET");
            activity?.SetTag("url.path", "/smoke/observability");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        public static void EmitCounterMetric(string metricName, string runId)
        {
            using var meter = new Meter(ObservabilityConstants.BusinessMeterName);
            var counter = meter.CreateCounter<long>(metricName);
            counter.Add(1,
                new KeyValuePair<string, object?>("smoke.test", true),
                new KeyValuePair<string, object?>("smoke.run_id", runId));
        }

        public static void Flush(ServiceProvider serviceProvider)
        {
            serviceProvider.GetRequiredService<TracerProvider>().ForceFlush();
            serviceProvider.GetRequiredService<MeterProvider>().ForceFlush();
        }
    }
}
