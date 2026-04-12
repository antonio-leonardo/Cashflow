using Cashflow.Functions.Balance;
using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Functions.Domain.Tests
{
    public class BalanceServiceBusFunctionTests
    {
        [Fact]
        public async Task RunAsync_ShouldDispatch_Envelope_ToProcessor()
        {
            var envelope = BuildEnvelope();
            var messageBody = JsonSerializer.Serialize(envelope);
            var processor = new Mock<ITransactionEventProcessor<TransactionCreatedEventV1>>();
            var logger = new Mock<ILogger<BalanceServiceBusFunction>>();
            var sut = new BalanceServiceBusFunction(processor.Object, logger.Object);

            await sut.RunAsync(messageBody, Mock.Of<FunctionContext>(), CancellationToken.None);

            processor.Verify(p => p.ProcessAsync(
                It.Is<EventEnvelope<TransactionCreatedEventV1>>(e =>
                    e.Event.TransactionId == envelope.Event.TransactionId &&
                    e.Event.AccountId == envelope.Event.AccountId &&
                    e.Metadata.CorrelationId == envelope.Metadata.CorrelationId),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RunAsync_ShouldIgnore_InvalidPayload()
        {
            var processor = new Mock<ITransactionEventProcessor<TransactionCreatedEventV1>>();
            var logger = new Mock<ILogger<BalanceServiceBusFunction>>();
            var sut = new BalanceServiceBusFunction(processor.Object, logger.Object);

            await sut.RunAsync("{ invalid json", Mock.Of<FunctionContext>(), CancellationToken.None);

            processor.Verify(p => p.ProcessAsync(It.IsAny<EventEnvelope<TransactionCreatedEventV1>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static EventEnvelope<TransactionCreatedEventV1> BuildEnvelope()
        {
            var evt = new TransactionCreatedEventV1(
                transactionId: Guid.NewGuid(),
                accountId: Guid.NewGuid(),
                amount: 150m,
                currency: "BRL",
                type: TransactionType.Credit);

            return new EventEnvelope<TransactionCreatedEventV1>(
                evt,
                new MessageMetadata(
                    CorrelationId: Guid.NewGuid().ToString(),
                    CausationId: evt.EventId.ToString(),
                    Source: nameof(BalanceServiceBusFunctionTests),
                    TenantId: null,
                    CreatedAtUtc: DateTime.UtcNow));
        }
    }
}
