using Cashflow.Back.End.Shared.Events;

namespace Cashflow.Back.End.Shared.Messaging.Abstractions
{
    public interface IMessageSubscriber
    {
        Task SubscribeAsync<TEvent>(
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent;
    }
}