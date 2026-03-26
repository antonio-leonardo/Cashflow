using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace Cashflow.Shared.Observability
{
    public static class OpenTelemetryServiceCollectionExtensions
    {
        public static IServiceCollection AddCashflowOpenTelemetryForWeb(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName)
        {
            return AddCashflowOpenTelemetry(services, configuration, serviceName, includeAspNetCore: true);
        }

        public static IServiceCollection AddCashflowOpenTelemetryForWorker(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName)
        {
            return AddCashflowOpenTelemetry(services, configuration, serviceName, includeAspNetCore: false);
        }

        private static IServiceCollection AddCashflowOpenTelemetry(
            IServiceCollection services,
            IConfiguration configuration,
            string serviceName,
            bool includeAspNetCore)
        {
            var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

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

            openTelemetry.WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(ObservabilityConstants.MessagingActivitySourceName)
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });

                if (includeAspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });
                }

                AddTraceExporter(tracing, configuration);
            });

            openTelemetry.WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(ObservabilityConstants.MessagingMeterName)
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation();

                if (includeAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                AddMetricsExporter(metrics, configuration);
            });

            return services;
        }

        private static void AddTraceExporter(TracerProviderBuilder tracing, IConfiguration configuration)
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

        private static void AddMetricsExporter(MeterProviderBuilder metrics, IConfiguration configuration)
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
    }
}
