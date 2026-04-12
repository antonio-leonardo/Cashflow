using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;

namespace Cashflow.Worker.Report
{
    /// <summary>
    /// Host-agnostic processor: appends a transaction to the MongoDB report projection.
    /// Can be invoked by the BackgroundService worker or by an Azure Function trigger.
    /// </summary>
    public sealed class ReportEventProcessor : ITransactionEventProcessor<TransactionCreatedEventV1>
    {
        private readonly IServiceProvider _services;

        public ReportEventProcessor(IServiceProvider services)
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
