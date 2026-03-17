using Cashflow.Service.Transaction.Domain;
using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Shared.Events;
using Cashflow.Shared.Logging;
using Cashflow.Shared.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cashflow.Outbox.Worker
{
    public class Worker : BackgroundService
    {
        private readonly IServiceProvider _provider;
        private readonly IMessageBus _messageBus;

        private const int BatchSize = 50;

        public Worker(
            IServiceProvider provider,
            IMessageBus messageBus)
        {
            _provider = provider;
            _messageBus = messageBus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _provider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider
                        .GetRequiredService<TransactionDbContext>();

                    var logService = scope.ServiceProvider
                        .GetRequiredService<ILogService>();

                    var pending = await dbContext.OutboxEvents
                        .Where(e => e.ProcessedAt == null)
                        .OrderBy(e => e.CreatedAt)
                        .Take(BatchSize)
                        .ToListAsync(stoppingToken);

                    if (pending.Count == 0)
                    {
                        await Task.Delay(2000, stoppingToken);
                        continue;
                    }

                    foreach (var outboxEvent in pending)
                    {
                        try
                        {
                            var eventType = ResolveEventType(outboxEvent.EventType);

                            var deserialized = (IEvent?)JsonSerializer.Deserialize(
                                outboxEvent.Payload,
                                eventType!);

                            var metadata = new MessageMetadata(
                                outboxEvent.EventId.ToString(),
                                outboxEvent.EventId.ToString(),
                                "OutboxWorker",
                                null,
                                DateTime.UtcNow);

                            var envelopeType = typeof(EventEnvelope<>).MakeGenericType(eventType!);
                            var envelope = Activator.CreateInstance(envelopeType, deserialized!, metadata)!;
                            await _messageBus.PublishAsync((dynamic)envelope, stoppingToken);

                            outboxEvent.ProcessedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            logService.Log(
                                Cashflow.Shared.Logging.LogLevel.Error,
                                "Erro ao processar outbox",
                                new LogContext("OutboxWorker", null, null, null),
                                ex);
                        }
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
        }

        private static Type? ResolveEventType(string eventTypeName)
        {
            // Por enquanto, lidamos apenas com eventos do domínio de Transações.
            return typeof(TransactionCreatedEventV1).Assembly.GetType(
                $"Cashflow.Service.Transaction.Domain.{eventTypeName}");
        }

        //private void LogError(string message, Exception? exception)
        //{
        //    var context = new LogContext(
        //        "OutboxWorker",
        //        null,
        //        null,
        //        null);

        //    _logService.Log(
        //        Cashflow.Shared.Logging.LogLevel.Error,
        //        message,
        //        context,
        //        exception);
        //}
    }
}