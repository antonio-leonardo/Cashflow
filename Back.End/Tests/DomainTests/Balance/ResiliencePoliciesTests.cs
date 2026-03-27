using Cashflow.Shared.Resilience;
using System.Net;

namespace Balance.Domain.Tests;

public class ResiliencePoliciesTests
{
    [Fact]
    public async Task GetHttpResiliencePolicy_ShouldReturnConfiguredFallback_WhenEnabled()
    {
        var options = new HttpResiliencePolicyOptions
        {
            RetryCount = 1,
            RetryBaseDelayMilliseconds = 1,
            CircuitBreakerFailureThreshold = 10,
            CircuitBreakerBreakSeconds = 1,
            BulkheadParallelization = 5,
            BulkheadQueue = 5,
            TimeoutSeconds = 2,
            EnableFallback = true,
            RetryOnAnyNonSuccessStatusCode = true,
            FallbackStatusCode = (int)HttpStatusCode.BadGateway,
            FallbackMessage = "fallback-http"
        };

        var policy = ResiliencePolicies.GetHttpResiliencePolicy(options);
        var response = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("fallback-http", content);
    }

    [Fact]
    public async Task GetHttpResiliencePolicy_ShouldKeepOriginalStatus_WhenFallbackDisabled()
    {
        var options = new HttpResiliencePolicyOptions
        {
            RetryCount = 1,
            RetryBaseDelayMilliseconds = 1,
            CircuitBreakerFailureThreshold = 10,
            CircuitBreakerBreakSeconds = 1,
            BulkheadParallelization = 5,
            BulkheadQueue = 5,
            TimeoutSeconds = 2,
            EnableFallback = false,
            RetryOnAnyNonSuccessStatusCode = true
        };

        var policy = ResiliencePolicies.GetHttpResiliencePolicy(options);
        var response = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetResiliencePolicy_ShouldSanitizeInvalidOptions_AndExecuteWithoutFallback()
    {
        var options = new ResiliencePolicyOptions
        {
            RetryCount = 0,
            RetryBaseDelayMilliseconds = 0,
            CircuitBreakerFailureThreshold = 0,
            CircuitBreakerBreakSeconds = 0,
            BulkheadParallelization = 0,
            BulkheadQueue = -1,
            TimeoutSeconds = 0,
            EnableFallback = false
        };

        var policy = ResiliencePolicies.GetResiliencePolicy(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(() =>
            throw new InvalidOperationException("forced")));
    }
}
