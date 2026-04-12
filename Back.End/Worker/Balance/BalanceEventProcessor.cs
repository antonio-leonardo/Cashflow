using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;

namespace Cashflow.Worker.Balance
{
    /// <summary>
    /// Host-agnostic processor: applies a transaction to the Redis balance projection.
    /// Can be invoked by the BackgroundService worker or by an Azure Function trigger.
    /// </summary>
    public sealed class BalanceEventProcessor : ITransactionEventProcessor<TransactionCreatedEventV1>
    {
        private readonly IServiceProvider _services;

        public BalanceEventProcessor(IServiceProvider services)
        {
            _services = services;
        }

        public async Task ProcessAsync(
            EventEnvelope<TransactionCreatedEventV1> envelope,
            CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TransactionCreatedHandler>();
            await handler.HandleAsync(
                envelope.Event,
                envelope.Event.EventId.ToString("N"),
                cancellationToken);
        }
    }
}
