using Cashflow.Shared.Events;
using Cashflow.Shared.Logging;
using Cashflow.Shared.Messaging.Abstractions;

namespace Cashflow.Outbox.Worker
{
    public sealed class ConsoleMessageBus : IMessageBus
    {
        private readonly ILogService _logService;

        public ConsoleMessageBus(ILogService logService)
        {
            _logService = logService;
        }

        public Task PublishAsync<TEvent>(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var context = new LogContext(
                ServiceName: "OutboxWorker",
                CorrelationId: envelope.Metadata.CorrelationId,
                TransactionId: (envelope.Event as dynamic)?.TransactionId?.ToString(),
                UserId: null);

            _logService.Log(
                Cashflow.Shared.Logging.LogLevel.Information,
                $"Publishing event {envelope.Event.GetType().Name}",
                context);

            return Task.CompletedTask;
        }

        public Task SubscribeAsync<TEvent>(
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            // Não é utilizado pelo outbox worker.
            return Task.CompletedTask;
        }
    }
}