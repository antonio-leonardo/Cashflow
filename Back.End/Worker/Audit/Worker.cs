using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Back.End.Shared.Contracts.Idempotency;
using Cashflow.Back.End.Shared.Logging;
using Cashflow.Back.End.Shared.Messaging.Abstractions;

namespace Cashflow.Audit.Worker
{
    /// <summary>
    /// Worker de auditoria
    /// Consumidor idempotente
    /// </summary>
    public class Worker(
        IMessageBus messageBus,
        IProcessedEventStore processedEventStore,
        ILogService logService) : BackgroundService
    {
        private const string ConsumerName = "Audit.Worker";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await messageBus.SubscribeAsync<TransactionCreatedEventV1>(HandleTransactionCreatedAsync, stoppingToken);
        }

        private async Task HandleTransactionCreatedAsync(
            EventEnvelope<TransactionCreatedEventV1> envelope,
            CancellationToken cancellationToken)
        {
            var evt = envelope.Event;
            if (await processedEventStore.WasProcessedAsync(evt.EventId, ConsumerName, cancellationToken))
                return;

            var context = new LogContext(
                ServiceName: ConsumerName,
                CorrelationId: envelope.Metadata.CorrelationId,
                TransactionId: evt.TransactionId.ToString(),
                UserId: null);
            logService.Log(
                Cashflow.Back.End.Shared.Logging.LogLevel.Information,
                "Audit logged for transaction {TransactionId}.",
                context,
                additionalData: new Dictionary<string, object>
                {
                    ["TransactionId"] = evt.TransactionId,
                    ["AccountId"] = evt.AccountId,
                    ["Amount"] = evt.Amount,
                    ["Currency"] = evt.Currency,
                    ["OccurredAt"] = envelope.Event.OccurredAt
                });

            await processedEventStore.MarkProcessedAsync(evt.EventId, ConsumerName, cancellationToken);
        }
    }
}