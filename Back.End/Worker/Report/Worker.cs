using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;

namespace Cashflow.Worker.Report
{
    public class Worker : BackgroundService
    {
        private readonly IMessageBus _bus;
        private readonly IServiceProvider _provider;

        public Worker(IMessageBus bus, IServiceProvider provider)
        {
            _bus = bus;
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _bus.SubscribeAsync<TransactionCreatedEventV1>(
                HandleTransactionCreated,
                stoppingToken);
        }

        private async Task HandleTransactionCreated(
            EventEnvelope<TransactionCreatedEventV1> envelope,
            CancellationToken ct)
        {
            using (var scope = _provider.CreateScope())
            {
                var handler = scope.ServiceProvider
                    .GetRequiredService<TransactionCreatedHandler>();

                await handler.HandleAsync(envelope.Event, ct);
            }
        }
    }
}