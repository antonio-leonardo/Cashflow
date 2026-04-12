using Azure.Monitor.OpenTelemetry.AspNetCore;
using Cashflow.Shared.Contracts.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace Cashflow.Shared.Observability
{
    public static class OpenTelemetryWebServiceCollectionExtensions
    {
        public static IServiceCollection AddCashflowOpenTelemetryForWeb(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName)
        {
            var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
            var telemetryProvider = configuration.GetConfiguredProvider(
                "Providers:Telemetry",
                TelemetryProvider.Jaeger,
                "telemetry provider");
            var applicationInsightsConnectionString = ResolveApplicationInsightsConnectionString(configuration);

            if (telemetryProvider is TelemetryProvider.ApplicationInsights &&
                string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
            {
                throw new InvalidOperationException(
                    "Configuration 'ApplicationInsights:ConnectionString' or environment variable 'APPLICATIONINSIGHTS_CONNECTION_STRING' is required when 'Providers:Telemetry' is set to 'ApplicationInsights'.");
            }

            var openTelemetry = services.AddOpenTelemetry()
                .ConfigureResource(resource =>
                {
                    resource
                        .AddService(
                            serviceName: serviceName,
                            serviceVersion: serviceVersion,
                            serviceInstanceId: Environment.MachineName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown"
                        });
                });

            switch (telemetryProvider)
            {
                case TelemetryProvider.ApplicationInsights:
                    openTelemetry.UseAzureMonitor(options =>
                    {
                        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
                        {
                            options.ConnectionString = applicationInsightsConnectionString;
                        }
                    });

                    openTelemetry.WithTracing(tracing =>
                        tracing
                            .AddSource(ObservabilityConstants.BusinessActivitySourceName)
                            .AddSource(ObservabilityConstants.MessagingActivitySourceName)
                            .AddHttpClientInstrumentation(o => o.RecordException = true)
                            .AddAspNetCoreInstrumentation(o => o.RecordException = true));

                    openTelemetry.WithMetrics(metrics =>
                        metrics
                            .AddMeter(ObservabilityConstants.BusinessMeterName)
                            .AddMeter(ObservabilityConstants.MessagingMeterName)
                            .AddRuntimeInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddAspNetCoreInstrumentation());
                    break;

                case TelemetryProvider.Jaeger:
                case TelemetryProvider.Otlp:
                    openTelemetry.WithTracing(tracing =>
                    {
                        tracing
                            .SetSampler(new AlwaysOnSampler())
                            .AddSource(ObservabilityConstants.BusinessActivitySourceName)
                            .AddSource(ObservabilityConstants.MessagingActivitySourceName)
                            .AddHttpClientInstrumentation(options => options.RecordException = true)
                            .AddAspNetCoreInstrumentation(options => options.RecordException = true);

                        AddOtlpTraceExporter(tracing, configuration);
                    });

                    openTelemetry.WithMetrics(metrics =>
                    {
                        metrics
                            .AddMeter(ObservabilityConstants.BusinessMeterName)
                            .AddMeter(ObservabilityConstants.MessagingMeterName)
                            .AddRuntimeInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddAspNetCoreInstrumentation();

                        AddOtlpMetricsExporter(metrics, configuration);
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported telemetry provider '{telemetryProvider}' configured at 'Providers:Telemetry'.");
            }

            return services;
        }

        private static void AddOtlpTraceExporter(TracerProviderBuilder tracing, IConfiguration configuration)
        {
            if (!TryGetOtlpEndpoint(configuration, out var endpoint))
            {
                return;
            }

            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = endpoint;
                options.Protocol = OtlpExportProtocol.Grpc;
            });
        }

        private static void AddOtlpMetricsExporter(MeterProviderBuilder metrics, IConfiguration configuration)
        {
            if (!TryGetOtlpEndpoint(configuration, out var endpoint))
            {
                return;
            }

            metrics.AddOtlpExporter(options =>
            {
                options.Endpoint = endpoint;
                options.Protocol = OtlpExportProtocol.Grpc;
            });
        }

        private static bool TryGetOtlpEndpoint(IConfiguration configuration, out Uri endpoint)
        {
            var endpointValue =
                configuration["OpenTelemetry:Otlp:Endpoint"] ??
                configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

            if (Uri.TryCreate(endpointValue, UriKind.Absolute, out endpoint!))
            {
                return true;
            }

            endpoint = default!;
            return false;
        }

        private static string? ResolveApplicationInsightsConnectionString(IConfiguration configuration)
        {
            return configuration["ApplicationInsights:ConnectionString"]
                ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        }
    }
}
