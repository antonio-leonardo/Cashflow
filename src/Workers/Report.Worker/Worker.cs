using Cashflow.Shared.Contracts.Idempotency;
using Cashflow.Shared.Logging;
using Cashflow.Shared.Messaging;
using Cashflow.Transaction.Domain.Events;

namespace Cashflow.Report.Worker;

/// <summary>
/// Worker de leitura CQRS: Read Model Report / analítico (especificação §7). Consumidor idempotente (§13).
/// </summary>
public class Worker(
    IMessageBus messageBus,
    IProcessedEventStore processedEventStore,
    ILogService logService) : BackgroundService
{
    private const string ConsumerName = "Report.Worker";

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
            Cashflow.Shared.Logging.LogLevel.Information,
            "Report updated for transaction {TransactionId}, account {AccountId}.",
            context,
            additionalData: new Dictionary<string, object>
            {
                ["TransactionId"] = evt.TransactionId,
                ["AccountId"] = evt.AccountId,
                ["Amount"] = evt.Amount
            });

        await processedEventStore.MarkProcessedAsync(evt.EventId, ConsumerName, cancellationToken);
    }
}