using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;

namespace Cashflow.Worker.Audit
{
    /// <summary>
    /// Host-agnostic processor: records a transaction event in the MongoDB audit log.
    /// Can be invoked by the BackgroundService worker or by an Azure Function trigger.
    /// </summary>
    public sealed class AuditEventProcessor : ITransactionEventProcessor<TransactionCreatedEventV1>
    {
        private readonly IServiceProvider _services;

        public AuditEventProcessor(IServiceProvider services)
        {
            _services = services;
        }

        public async Task ProcessAsync(
            EventEnvelope<TransactionCreatedEventV1> envelope,
            CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TransactionCreatedHandler>();
            await handler.HandleAsync(envelope.Event, cancellationToken);
        }
    }
}
