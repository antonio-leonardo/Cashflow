using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Back.End.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Back.End.Shared.Events;
using Cashflow.Back.End.Shared.Logging;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cashflow.Back.End.Outbox.Worker
{
    public class Worker(
    TransactionDbContext dbContext,
    IMessageBus messageBus,
    ILogService logService) : BackgroundService
    {
        private const int BatchSize = 50;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pending = await dbContext.OutboxEvents
                    .Where(e => e.ProcessedAt == null)
                    .OrderBy(e => e.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (pending.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var outboxEvent in pending)
                {
                    try
                    {
                        var eventType = ResolveEventType(outboxEvent.EventType);
                        if (eventType is null)
                        {
                            LogError($"Unknown event type '{outboxEvent.EventType}' in outbox.", null);
                            outboxEvent.ProcessedAt = DateTime.UtcNow;
                            continue;
                        }

                        var deserialized = (IEvent?)JsonSerializer.Deserialize(
                            outboxEvent.Payload,
                            eventType);

                        if (deserialized is null)
                        {
                            LogError($"Failed to deserialize payload for event '{outboxEvent.EventType}'.", null);
                            outboxEvent.ProcessedAt = DateTime.UtcNow;
                            continue;
                        }

                        var metadata = new MessageMetadata(
                            CorrelationId: outboxEvent.EventId.ToString(),
                            CausationId: outboxEvent.EventId.ToString(),
                            Source: "OutboxWorker",
                            TenantId: null,
                            CreatedAtUtc: DateTime.UtcNow);

                        var envelopeType = typeof(EventEnvelope<>).MakeGenericType(eventType);
                        var envelope = Activator.CreateInstance(envelopeType, deserialized, metadata)!;

                        var publishMethod = typeof(IMessagePublisher)
                            .GetMethod(nameof(IMessagePublisher.PublishAsync))!
                            .MakeGenericMethod(eventType);

                        await (Task)publishMethod.Invoke(messageBus, new[] { envelope, stoppingToken })!;

                        outboxEvent.ProcessedAt = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        LogError("Error processing outbox event.", ex);
                    }
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }

        private static Type? ResolveEventType(string eventTypeName)
        {
            // Por enquanto, lidamos apenas com eventos do domínio de Transações.
            return typeof(TransactionCreatedEventV1).Assembly.GetType(
                $"Cashflow.Transaction.Domain.Events.{eventTypeName}");
        }

        private void LogError(string message, Exception? exception)
        {
            var context = new LogContext(
                ServiceName: "OutboxWorker",
                CorrelationId: null,
                TransactionId: null,
                UserId: null);

            logService.Log(
                Cashflow.Back.End.Shared.Logging.LogLevel.Error,
                message,
                context,
                exception);
        }
    }
}