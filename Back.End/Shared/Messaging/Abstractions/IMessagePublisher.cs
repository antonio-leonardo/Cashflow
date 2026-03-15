using Cashflow.Back.End.Shared.Events;

namespace Cashflow.Back.End.Shared.Messaging.Abstractions
{
    public interface IMessagePublisher
    {
        Task PublishAsync<TEvent>(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
            where TEvent : IEvent;
    }
}