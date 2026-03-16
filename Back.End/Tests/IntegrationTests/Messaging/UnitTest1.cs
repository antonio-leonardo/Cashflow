using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Back.End.Shared.Messaging.Abstractions;

namespace Messaging.Integration.Tests;

public class UnitTest1
{
    [Fact]
    public void Should_Create_EventEnvelope()
    {
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            "USD");

        var metadata = new MessageMetadata(
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: evt.EventId.ToString(),
            Source: "MessagingTests",
            TenantId: null,
            CreatedAtUtc: DateTime.UtcNow);

        var envelope = new EventEnvelope<TransactionCreatedEventV1>(
            evt,
            metadata);

        Assert.NotNull(envelope);
        Assert.Equal(evt, envelope.Event);
        Assert.Equal("MessagingTests", envelope.Metadata.Source);
        Assert.Equal(evt.EventId.ToString(), envelope.Metadata.CausationId);
    }
}
