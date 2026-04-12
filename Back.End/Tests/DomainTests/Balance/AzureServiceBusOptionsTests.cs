using Cashflow.Shared.Messaging.AzureServiceBus.MessageBus;

namespace Balance.Domain.Tests;

/// <summary>
/// Pure unit tests for <see cref="AzureServiceBusOptions"/> default values and invariants.
/// No Docker or external infrastructure required.
/// </summary>
public class AzureServiceBusOptionsTests
{
    [Fact]
    public void DefaultOptions_PrefetchCount_IsZero()
    {
        var options = new AzureServiceBusOptions();
        Assert.Equal(0, options.PrefetchCount);
    }

    [Fact]
    public void DefaultOptions_MaxConcurrentCalls_IsOne()
    {
        var options = new AzureServiceBusOptions();
        Assert.Equal(1, options.MaxConcurrentCalls);
    }

    [Fact]
    public void DefaultOptions_MaxAutoLockRenewalSeconds_IsFiveMinutes()
    {
        var options = new AzureServiceBusOptions();
        Assert.Equal(300, options.MaxAutoLockRenewalSeconds);
    }

    [Fact]
    public void DefaultOptions_EnableSessions_IsFalse()
    {
        var options = new AzureServiceBusOptions();
        Assert.False(options.EnableSessions);
    }

    [Fact]
    public void DefaultOptions_UseManagedIdentity_IsFalse()
    {
        var options = new AzureServiceBusOptions();
        Assert.False(options.UseManagedIdentity);
    }

    [Fact]
    public void DefaultOptions_ConnectionString_IsNull()
    {
        var options = new AzureServiceBusOptions();
        Assert.Null(options.ConnectionString);
    }

    [Fact]
    public void DefaultOptions_Namespace_IsNull()
    {
        var options = new AzureServiceBusOptions();
        Assert.Null(options.Namespace);
    }

    [Fact]
    public void DefaultOptions_ConsumerName_IsEmptyString()
    {
        var options = new AzureServiceBusOptions();
        Assert.Equal(string.Empty, options.ConsumerName);
    }

    /// <summary>
    /// Verifies that MaxDeliveryCount is NOT a property of the options class.
    /// It is a server-side property of the queue/subscription entity and must be
    /// configured via ARM/Bicep/portal — not from the client SDK.
    /// </summary>
    [Fact]
    public void Options_DoesNotExposeMaxDeliveryCount()
    {
        var type = typeof(AzureServiceBusOptions);
        var property = type.GetProperty("MaxDeliveryCount");
        Assert.Null(property);
    }

    [Fact]
    public void Options_EnableSessions_CanBeSetToTrue()
    {
        var options = new AzureServiceBusOptions { EnableSessions = true };
        Assert.True(options.EnableSessions);
    }

    [Fact]
    public void Options_MaxAutoLockRenewalSeconds_CanBeSetToZeroToDisableAutoRenewal()
    {
        var options = new AzureServiceBusOptions { MaxAutoLockRenewalSeconds = 0 };
        Assert.Equal(0, options.MaxAutoLockRenewalSeconds);
    }
}
