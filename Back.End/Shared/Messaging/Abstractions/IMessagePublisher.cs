using Cashflow.Shared.Events;

namespace Cashflow.Shared.Messaging.Abstractions
{
    public interface IMessagePublisher
    {
        Task PublishAsync<TEvent>(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
            where TEvent : IEvent;
    }
}