using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;

namespace Messaging.Integration.Tests;

public class MessagingIntegration
{
    [Fact]
    public void Should_Create_EventEnvelope()
    {
        var evt = new TransactionCreatedEventV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            "BRL");

        var metadata = new MessageMetadata(
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: evt.EventId.ToString(),
            Source: "MessagingTests",
            TenantId: null,
            CreatedAtUtc: DateTime.UtcNow);

        var envelope = new EventEnvelope<TransactionCreatedEventV1>(
            evt,
            metadata);

        Xunit.Assert.NotNull(envelope);
        Xunit.Assert.Equal(evt, envelope.Event);
        Xunit.Assert.Equal("MessagingTests", envelope.Metadata.Source);
        Xunit.Assert.Equal(evt.EventId.ToString(), envelope.Metadata.CausationId);
    }
}