using Cashflow.Shared.Events;

namespace Cashflow.Shared.Messaging
{
    public sealed record EventEnvelope<TEvent>(TEvent Event, MessageMetadata Metadata)
    where TEvent : IEvent;
}