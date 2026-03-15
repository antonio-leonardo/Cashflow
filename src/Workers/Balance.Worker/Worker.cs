using Cashflow.Shared.Contracts.Idempotency;
using Cashflow.Shared.Logging;
using Cashflow.Shared.Messaging;
using Cashflow.Transaction.Domain.Events;

namespace Cashflow.Balance.Worker
{
    /// <summary>
    /// Worker de leitura CQRS: Read Model Balance
    /// Consumidor idempotente
    /// </summary>
    public class Worker(
        IMessageBus messageBus,
        IProcessedEventStore processedEventStore,
        ILogService logService) : BackgroundService
    {
        private const string ConsumerName = "Balance.Worker";

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

            // Read model: atualizar saldo (skeleton: apenas log)
            var context = new LogContext(
                ServiceName: ConsumerName,
                CorrelationId: envelope.Metadata.CorrelationId,
                TransactionId: evt.TransactionId.ToString(),
                UserId: null);
            logService.Log(
                Cashflow.Shared.Logging.LogLevel.Information,
                "Balance updated for account {AccountId}, amount {Amount} {Currency}.",
                context,
                additionalData: new Dictionary<string, object>
                {
                    ["AccountId"] = evt.AccountId,
                    ["Amount"] = evt.Amount,
                    ["Currency"] = evt.Currency
                });

            await processedEventStore.MarkProcessedAsync(evt.EventId, ConsumerName, cancellationToken);
        }
    }
}