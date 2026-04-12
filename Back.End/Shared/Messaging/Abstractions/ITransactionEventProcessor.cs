using Cashflow.Shared.Events;

namespace Cashflow.Shared.Messaging.Abstractions
{
    /// <summary>
    /// Host-agnostic contract for processing a single event envelope.
    /// Implementations contain the business logic of each worker; the host
    /// (BackgroundService or Azure Function) is responsible only for receiving
    /// the raw message and invoking this processor.
    /// </summary>
    public interface ITransactionEventProcessor<TEvent>
        where TEvent : IEvent
    {
        Task ProcessAsync(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken);
    }
}
