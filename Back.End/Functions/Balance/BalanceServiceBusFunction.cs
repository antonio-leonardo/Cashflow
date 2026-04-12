using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cashflow.Functions.Balance
{
    public class BalanceServiceBusFunction
    {
        private readonly ITransactionEventProcessor<TransactionCreatedEventV1> _processor;
        private readonly ILogger<BalanceServiceBusFunction> _logger;

        public BalanceServiceBusFunction(
            ITransactionEventProcessor<TransactionCreatedEventV1> processor,
            ILogger<BalanceServiceBusFunction> logger)
        {
            _processor = processor;
            _logger    = logger;
        }

        /// <summary>
        /// Service Bus Trigger for the balance read-model projection.
        /// Topic and subscription names follow the convention documented in
        /// Shared/Messaging/MESSAGING_CONVENTIONS.md.
        ///
        /// Environment variables required:
        ///   AzureServiceBus__ConnectionString  OR  AzureServiceBus__Namespace (+ Managed Identity)
        ///   AzureServiceBus__ConsumerName      — set to "balance-worker"
        /// </summary>
        [Function(nameof(BalanceServiceBusFunction))]
        public async Task RunAsync(
            [ServiceBusTrigger(
                topicName: "%AzureServiceBus__TopicName%",
                subscriptionName: "%AzureServiceBus__ConsumerName%",
                Connection = "AzureServiceBus")]
            string messageBody,
            FunctionContext context,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("BalanceFunction triggered. MessageLength={Length}", messageBody.Length);

            EventEnvelope<TransactionCreatedEventV1>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<EventEnvelope<TransactionCreatedEventV1>>(
                    messageBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Received invalid JSON payload on BalanceFunction - message skipped.");
                return;
            }

            if (envelope is null)
            {
                _logger.LogWarning("Received null or unparseable envelope — message skipped.");
                return;
            }

            await _processor.ProcessAsync(envelope, cancellationToken);
        }
    }
}
