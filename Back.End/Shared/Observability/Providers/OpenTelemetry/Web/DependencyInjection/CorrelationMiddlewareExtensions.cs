using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Cashflow.Shared.Observability
{
    public static class CorrelationMiddlewareExtensions
    {
        public static IApplicationBuilder UseCashflowCorrelationId(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var correlationId = ResolveCorrelationId(context.Request);

                context.TraceIdentifier = correlationId;
                context.Request.Headers[ObservabilityConstants.CorrelationIdHeaderName] = correlationId;
                context.Response.Headers[ObservabilityConstants.CorrelationIdHeaderName] = correlationId;

                Activity.Current?.SetTag("correlation.id", correlationId);
                Activity.Current?.AddBaggage("correlation.id", correlationId);

                await next();
            });
        }

        private static string ResolveCorrelationId(HttpRequest request)
        {
            var incoming = request.Headers[ObservabilityConstants.CorrelationIdHeaderName].FirstOrDefault();
            if (Guid.TryParse(incoming, out var parsed))
            {
                return parsed.ToString("D");
            }

            return Guid.NewGuid().ToString("D");
        }
    }
}
