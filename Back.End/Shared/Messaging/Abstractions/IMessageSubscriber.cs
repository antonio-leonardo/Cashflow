using Cashflow.Shared.Events;

namespace Cashflow.Shared.Messaging.Abstractions
{
    public interface IMessageSubscriber
    {
        Task SubscribeAsync<TEvent>(
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent;
    }
}