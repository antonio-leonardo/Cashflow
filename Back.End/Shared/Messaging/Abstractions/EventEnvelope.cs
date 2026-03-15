using Cashflow.Back.End.Shared.Events;

namespace Cashflow.Back.End.Shared.Messaging.Abstractions
{
    public sealed record EventEnvelope<TEvent>(TEvent Event, MessageMetadata Metadata)
    where TEvent : IEvent;
}