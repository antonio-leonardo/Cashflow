using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using Polly.Wrap;
using System.Diagnostics.Metrics;
using System.Net;

namespace Cashflow.Shared.Resilience
{
    public class ResiliencePolicyOptions
    {
        public int RetryCount { get; set; } = 3;
        public int RetryBaseDelayMilliseconds { get; set; } = 200;
        public int CircuitBreakerFailureThreshold { get; set; } = 5;
        public int CircuitBreakerBreakSeconds { get; set; } = 20;
        public int BulkheadParallelization { get; set; } = 20;
        public int BulkheadQueue { get; set; } = 100;
        public int TimeoutSeconds { get; set; } = 5;
        public bool EnableFallback { get; set; } = false;
    }

    public sealed class HttpResiliencePolicyOptions : ResiliencePolicyOptions
    {
        public bool RetryOnAnyNonSuccessStatusCode { get; set; } = true;
        public int FallbackStatusCode { get; set; } = (int)HttpStatusCode.ServiceUnavailable;
        public string FallbackMessage { get; set; } = "Servico temporariamente indisponivel.";

        public HttpResiliencePolicyOptions()
        {
            EnableFallback = true;
        }
    }

    public static class ResiliencePolicies
    {
        private static readonly Meter ResilienceMeter = new("Cashflow.Resilience.Polly");
        private static readonly Counter<long> RetryCounter = ResilienceMeter.CreateCounter<long>("cashflow.resilience.retries");
        private static readonly Counter<long> FallbackCounter = ResilienceMeter.CreateCounter<long>("cashflow.resilience.fallbacks");
        private static readonly Counter<long> CircuitBreakCounter = ResilienceMeter.CreateCounter<long>("cashflow.resilience.circuit_breaks");

        public static AsyncPolicyWrap<object> GetResiliencePolicy(
            ResiliencePolicyOptions? options = null,
            ILogger? logger = null,
            string policyName = "default")
        {
            var cfg = Sanitize(options ?? new ResiliencePolicyOptions());

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: cfg.RetryCount,
                    sleepDurationProvider: retry => ExponentialBackoff(cfg.RetryBaseDelayMilliseconds, retry),
                    onRetry: (exception, delay, retryAttempt, _) =>
                    {
                        RetryCounter.Add(1, GetTags(policyName));
                        logger?.LogWarning(
                            exception,
                            "Resilience retry {PolicyName} attempt {RetryAttempt} after {DelayMs}ms.",
                            policyName,
                            retryAttempt,
                            delay.TotalMilliseconds);
                    });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: cfg.CircuitBreakerFailureThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(cfg.CircuitBreakerBreakSeconds),
                    onBreak: (exception, breakDelay) =>
                    {
                        CircuitBreakCounter.Add(1, GetTags(policyName));
                        logger?.LogWarning(
                            exception,
                            "Circuit breaker OPEN for {PolicyName}. Break duration: {BreakDurationMs}ms.",
                            policyName,
                            breakDelay.TotalMilliseconds);
                    },
                    onReset: () => logger?.LogInformation(
                        "Circuit breaker CLOSED (reset) for {PolicyName}.",
                        policyName),
                    onHalfOpen: () => logger?.LogInformation(
                        "Circuit breaker HALF-OPEN (probing) for {PolicyName}.",
                        policyName));

            var bulkheadPolicy = Policy
                .BulkheadAsync(
                    maxParallelization: cfg.BulkheadParallelization,
                    maxQueuingActions: cfg.BulkheadQueue);

            var timeoutPolicy = Policy
                .TimeoutAsync(
                    TimeSpan.FromSeconds(cfg.TimeoutSeconds),
                    TimeoutStrategy.Optimistic);

            IAsyncPolicy<object> fallbackPolicy = cfg.EnableFallback
                ? Policy<object>
                    .Handle<Exception>()
                    .FallbackAsync(
                        fallbackAction: async _ =>
                        {
                            FallbackCounter.Add(1, GetTags(policyName));
                            logger?.LogWarning(
                                "Resilience fallback executed for {PolicyName}.",
                                policyName);
                            await Task.CompletedTask;
                            return null!;
                        })
                : Policy.NoOpAsync<object>();

            return fallbackPolicy.WrapAsync(
                Policy.WrapAsync(
                    timeoutPolicy,
                    bulkheadPolicy,
                    circuitBreakerPolicy,
                    retryPolicy));
        }

        public static AsyncPolicyWrap<HttpResponseMessage> GetHttpResiliencePolicy(
            HttpResiliencePolicyOptions? options = null,
            ILogger? logger = null,
            string policyName = "http-default")
        {
            var cfg = SanitizeHttp(options ?? new HttpResiliencePolicyOptions());
            var statusCodeFallback = (HttpStatusCode)Math.Clamp(cfg.FallbackStatusCode, 100, 599);

            var policyBuilder = Policy<HttpResponseMessage>
                .Handle<Exception>();

            if (cfg.RetryOnAnyNonSuccessStatusCode)
            {
                policyBuilder = policyBuilder.OrResult(r => !r.IsSuccessStatusCode);
            }

            var retryPolicy = policyBuilder
                .WaitAndRetryAsync(
                    retryCount: cfg.RetryCount,
                    sleepDurationProvider: retry => ExponentialBackoff(cfg.RetryBaseDelayMilliseconds, retry),
                    onRetry: (outcome, delay, retryAttempt, _) =>
                    {
                        RetryCounter.Add(1, GetTags(policyName));
                        if (outcome.Exception is not null)
                        {
                            logger?.LogWarning(
                                outcome.Exception,
                                "HTTP resilience retry {PolicyName} attempt {RetryAttempt} after {DelayMs}ms.",
                                policyName,
                                retryAttempt,
                                delay.TotalMilliseconds);
                            return;
                        }

                        logger?.LogWarning(
                            "HTTP resilience retry {PolicyName} attempt {RetryAttempt} after {DelayMs}ms due to status {StatusCode}.",
                            policyName,
                            retryAttempt,
                            delay.TotalMilliseconds,
                            (int)outcome.Result.StatusCode);
                    });

            var circuitBreakerPolicy = policyBuilder
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: cfg.CircuitBreakerFailureThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(cfg.CircuitBreakerBreakSeconds),
                    onBreak: (outcome, breakDelay) =>
                    {
                        CircuitBreakCounter.Add(1, GetTags(policyName));
                        if (outcome.Exception is not null)
                        {
                            logger?.LogWarning(
                                outcome.Exception,
                                "HTTP circuit breaker OPEN for {PolicyName}. Break duration: {BreakDurationMs}ms.",
                                policyName,
                                breakDelay.TotalMilliseconds);
                            return;
                        }

                        logger?.LogWarning(
                            "HTTP circuit breaker OPEN for {PolicyName} due to status {StatusCode}. Break duration: {BreakDurationMs}ms.",
                            policyName,
                            (int)outcome.Result.StatusCode,
                            breakDelay.TotalMilliseconds);
                    },
                    onReset: () => logger?.LogInformation(
                        "HTTP circuit breaker CLOSED (reset) for {PolicyName}.",
                        policyName),
                    onHalfOpen: () => logger?.LogInformation(
                        "HTTP circuit breaker HALF-OPEN (probing) for {PolicyName}.",
                        policyName));

            var bulkheadPolicy = Policy
                .BulkheadAsync<HttpResponseMessage>(
                    maxParallelization: cfg.BulkheadParallelization,
                    maxQueuingActions: cfg.BulkheadQueue);

            var timeoutPolicy = Policy
                .TimeoutAsync<HttpResponseMessage>(
                    TimeSpan.FromSeconds(cfg.TimeoutSeconds),
                    TimeoutStrategy.Optimistic);

            IAsyncPolicy<HttpResponseMessage> fallbackPolicy = cfg.EnableFallback
                ? policyBuilder.FallbackAsync(
                    fallbackAction: async _ =>
                    {
                        FallbackCounter.Add(1, GetTags(policyName));
                        logger?.LogWarning(
                            "HTTP resilience fallback executed for {PolicyName}.",
                            policyName);
                        await Task.CompletedTask;
                        return new HttpResponseMessage(statusCodeFallback)
                        {
                            Content = new StringContent(cfg.FallbackMessage)
                        };
                    })
                : Policy.NoOpAsync<HttpResponseMessage>();

            return fallbackPolicy.WrapAsync(
                Policy.WrapAsync(
                    timeoutPolicy,
                    bulkheadPolicy,
                    circuitBreakerPolicy,
                    retryPolicy));
        }

        private static TimeSpan ExponentialBackoff(int baseDelayMilliseconds, int retryAttempt)
        {
            var multiplier = Math.Pow(2, retryAttempt);
            var delay = baseDelayMilliseconds * multiplier;
            return TimeSpan.FromMilliseconds(Math.Min(delay, 30_000d));
        }

        private static ResiliencePolicyOptions Sanitize(ResiliencePolicyOptions source)
        {
            return new ResiliencePolicyOptions
            {
                RetryCount = Math.Max(1, source.RetryCount),
                RetryBaseDelayMilliseconds = Math.Max(50, source.RetryBaseDelayMilliseconds),
                CircuitBreakerFailureThreshold = Math.Max(1, source.CircuitBreakerFailureThreshold),
                CircuitBreakerBreakSeconds = Math.Max(1, source.CircuitBreakerBreakSeconds),
                BulkheadParallelization = Math.Max(1, source.BulkheadParallelization),
                BulkheadQueue = Math.Max(0, source.BulkheadQueue),
                TimeoutSeconds = Math.Max(1, source.TimeoutSeconds),
                EnableFallback = source.EnableFallback
            };
        }

        private static HttpResiliencePolicyOptions SanitizeHttp(HttpResiliencePolicyOptions source)
        {
            var generic = Sanitize(source);
            return new HttpResiliencePolicyOptions
            {
                RetryCount = generic.RetryCount,
                RetryBaseDelayMilliseconds = generic.RetryBaseDelayMilliseconds,
                CircuitBreakerFailureThreshold = generic.CircuitBreakerFailureThreshold,
                CircuitBreakerBreakSeconds = generic.CircuitBreakerBreakSeconds,
                BulkheadParallelization = generic.BulkheadParallelization,
                BulkheadQueue = generic.BulkheadQueue,
                TimeoutSeconds = generic.TimeoutSeconds,
                EnableFallback = source.EnableFallback,
                RetryOnAnyNonSuccessStatusCode = source.RetryOnAnyNonSuccessStatusCode,
                FallbackStatusCode = source.FallbackStatusCode,
                FallbackMessage = string.IsNullOrWhiteSpace(source.FallbackMessage)
                    ? "Servico temporariamente indisponivel."
                    : source.FallbackMessage
            };
        }

        private static KeyValuePair<string, object?>[] GetTags(string policyName)
        {
            return new[]
            {
                new KeyValuePair<string, object?>("policy.name", policyName)
            };
        }
    }
}
