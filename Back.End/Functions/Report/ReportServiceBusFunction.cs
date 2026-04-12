using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cashflow.Functions.Report
{
    public class ReportServiceBusFunction
    {
        private readonly ITransactionEventProcessor<TransactionCreatedEventV1> _processor;
        private readonly ILogger<ReportServiceBusFunction> _logger;

        public ReportServiceBusFunction(
            ITransactionEventProcessor<TransactionCreatedEventV1> processor,
            ILogger<ReportServiceBusFunction> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        [Function(nameof(ReportServiceBusFunction))]
        public async Task RunAsync(
            [ServiceBusTrigger(
                topicName: "%AzureServiceBus__TopicName%",
                subscriptionName: "%AzureServiceBus__ConsumerName%",
                Connection = "AzureServiceBus")]
            string messageBody,
            FunctionContext context,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("ReportFunction triggered. MessageLength={Length}", messageBody.Length);

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
                    "Received invalid JSON payload on ReportFunction - message skipped.");
                return;
            }

            if (envelope is null)
            {
                _logger.LogWarning("Received null or unparseable envelope - message skipped.");
                return;
            }

            await _processor.ProcessAsync(envelope, cancellationToken);
        }
    }
}
