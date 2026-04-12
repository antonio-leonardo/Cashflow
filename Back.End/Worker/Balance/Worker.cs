using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;

namespace Cashflow.Worker.Balance
{
    /// <summary>
    /// BackgroundService adapter for the Balance read-model worker.
    /// Business logic lives in <see cref="BalanceEventProcessor"/>; this class
    /// only bridges IMessageBus.SubscribeAsync to the processor.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly IMessageBus _bus;
        private readonly ITransactionEventProcessor<TransactionCreatedEventV1> _processor;

        public Worker(
            IMessageBus bus,
            ITransactionEventProcessor<TransactionCreatedEventV1> processor)
        {
            _bus       = bus;
            _processor = processor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _bus.SubscribeAsync<TransactionCreatedEventV1>(
                _processor.ProcessAsync,
                stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
