using Polly;
using Polly.Timeout;
using Polly.Wrap;

namespace Cashflow.Shared.Resilience
{
    public static class ResiliencePolicies
    {
        public static AsyncPolicyWrap<object> GetResiliencePolicy()
        {
            // --- Retry ---
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retry => TimeSpan.FromSeconds(Math.Pow(2, retry)),
                    onRetry: (exception, time, retryCount, context) =>
                    {
                        Console.WriteLine($"[Retry {retryCount}] Esperando {time.TotalSeconds}s devido a: {exception?.Message}");
                    });

            // --- Circuit Breaker ---
            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, breakDelay) =>
                    {
                        Console.WriteLine($"[Circuit Breaker] Aberto por {breakDelay.TotalSeconds}s devido a: {ex?.Message}");
                    },
                    onReset: () => Console.WriteLine("[Circuit Breaker] Resetado"),
                    onHalfOpen: () => Console.WriteLine("[Circuit Breaker] Meio-aberto: Testando execução"));

            // --- Bulkhead ---
            var bulkheadPolicy = Policy
                .BulkheadAsync(
                    maxParallelization: 5,
                    maxQueuingActions: 10,
                    onBulkheadRejectedAsync: context =>
                    {
                        Console.WriteLine("[Bulkhead] Rejeitado por limite de paralelismo");
                        return Task.CompletedTask;
                    });

            // --- Timeout ---
            var timeoutPolicy = Policy
                .TimeoutAsync(
                    TimeSpan.FromSeconds(10),
                    TimeoutStrategy.Optimistic,
                    onTimeoutAsync: (context, timespan, task) =>
                    {
                        Console.WriteLine($"[Timeout] Excedeu {timespan.TotalSeconds}s");
                        return Task.CompletedTask;
                    });

            // --- Fallback (genérico para suportar Execute<TResult>) ---
            var fallbackPolicy = Policy<object>
                .Handle<Exception>()
                .FallbackAsync(
                    fallbackAction: async ct =>
                    {
                        Console.WriteLine("[Fallback] Executando ação alternativa...");
                        await Task.CompletedTask;
                        return null!;
                    },
                    onFallbackAsync: async outcome =>
                    {
                        Console.WriteLine($"[Fallback] Capturou exceção: {outcome.Exception?.Message}");
                        await Task.CompletedTask;
                    });

            // --- Wrap: o fallback genérico envolve as demais políticas não-genéricas ---
            var resiliencePolicy = fallbackPolicy.WrapAsync(
                Policy.WrapAsync(
                    timeoutPolicy,
                    bulkheadPolicy,
                    circuitBreakerPolicy,
                    retryPolicy));

            return resiliencePolicy;
        }

        public static AsyncPolicyWrap<HttpResponseMessage> GetHttpResiliencePolicy()
        {
            // --- Retry ---
            var retryPolicy = Policy<HttpResponseMessage>
                .Handle<Exception>()
                .OrResult(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retry => TimeSpan.FromSeconds(Math.Pow(2, retry)),
                    onRetry: (outcome, time, retryCount, context) =>
                    {
                        Console.WriteLine($"[Retry {retryCount}] Esperando {time.TotalSeconds}s | " +
                            $"Status: {outcome.Result?.StatusCode} | " +
                            $"Erro: {outcome.Exception?.Message}");
                    });

            // --- Circuit Breaker ---
            var circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<Exception>()
                .OrResult(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay) =>
                    {
                        Console.WriteLine($"[Circuit Breaker] Aberto por {breakDelay.TotalSeconds}s | " +
                            $"Status: {outcome.Result?.StatusCode} | " +
                            $"Erro: {outcome.Exception?.Message}");
                    },
                    onReset: () => Console.WriteLine("[Circuit Breaker] Resetado"),
                    onHalfOpen: () => Console.WriteLine("[Circuit Breaker] Meio-aberto: Testando execução"));

            // --- Bulkhead ---
            var bulkheadPolicy = Policy
                .BulkheadAsync<HttpResponseMessage>(
                    maxParallelization: 5,
                    maxQueuingActions: 10,
                    onBulkheadRejectedAsync: context =>
                    {
                        Console.WriteLine("[Bulkhead] Rejeitado por limite de paralelismo");
                        return Task.CompletedTask;
                    });

            // --- Timeout ---
            var timeoutPolicy = Policy
                .TimeoutAsync<HttpResponseMessage>(
                    TimeSpan.FromSeconds(10),
                    TimeoutStrategy.Optimistic,
                    onTimeoutAsync: (context, timespan, task) =>
                    {
                        Console.WriteLine($"[Timeout] Excedeu {timespan.TotalSeconds}s");
                        return Task.CompletedTask;
                    });

            // --- Fallback ---
            var fallbackPolicy = Policy<HttpResponseMessage>
                .Handle<Exception>()
                .OrResult(r => !r.IsSuccessStatusCode)
                .FallbackAsync(
                    fallbackAction: async ct =>
                    {
                        Console.WriteLine("[Fallback] Retornando resposta padrão...");
                        await Task.CompletedTask;
                        // Retorna um 503 controlado ao invés de lançar exception
                        return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new StringContent("Serviço temporariamente indisponível.")
                        };
                    },
                    onFallbackAsync: async outcome =>
                    {
                        Console.WriteLine($"[Fallback] Capturou: Status={outcome.Result?.StatusCode} | " +
                            $"Erro={outcome.Exception?.Message}");
                        await Task.CompletedTask;
                    });

            // --- Wrap ---
            return fallbackPolicy.WrapAsync(
                Policy.WrapAsync(
                    timeoutPolicy,
                    bulkheadPolicy,
                    circuitBreakerPolicy,
                    retryPolicy));
        }
    }
}