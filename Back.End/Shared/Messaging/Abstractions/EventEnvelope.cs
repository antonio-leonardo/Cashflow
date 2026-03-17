using Cashflow.Shared.Events;

namespace Cashflow.Shared.Messaging.Abstractions
{
    public sealed record EventEnvelope<TEvent>(TEvent Event, MessageMetadata Metadata)
    where TEvent : IEvent;
}