using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Infrastructure.Test;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging.Integration.Tests
{
    [Collection("RabbitMqCollection")]
    public class RabbitMqDecouplingIntegrationTests
    {
        private readonly RabbitMqContainerV1Fixture _rabbitMqFixture;

        public RabbitMqDecouplingIntegrationTests(RabbitMqContainerV1Fixture rabbitMqFixture)
        {
            _rabbitMqFixture = rabbitMqFixture;
        }

    [Fact]
        public async Task Should_FanOut_Event_To_Independent_Consumers()
        {
            var consumerA = CreateBus(consumerName: $"consumer-a-{Guid.NewGuid():N}", retryCount: 1, retryDelaySeconds: 1);
            var consumerB = CreateBus(consumerName: $"consumer-b-{Guid.NewGuid():N}", retryCount: 1, retryDelaySeconds: 1);
            var publisher = CreateBus(consumerName: string.Empty, retryCount: 1, retryDelaySeconds: 1);

            var receivedA = new TaskCompletionSource<EventEnvelope<TransactionCreatedEventV1>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var receivedB = new TaskCompletionSource<EventEnvelope<TransactionCreatedEventV1>>(TaskCreationOptions.RunContinuationsAsynchronously);

            await consumerA.SubscribeAsync<TransactionCreatedEventV1>((envelope, _) =>
            {
                receivedA.TrySetResult(envelope);
                return Task.CompletedTask;
            });

            await consumerB.SubscribeAsync<TransactionCreatedEventV1>((envelope, _) =>
            {
                receivedB.TrySetResult(envelope);
                return Task.CompletedTask;
            });

            await Task.Delay(500);

            var eventToPublish = new TransactionCreatedEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                "BRL",
                TransactionType.Credit);

            var correlationId = Guid.NewGuid().ToString();

            var envelope = new EventEnvelope<TransactionCreatedEventV1>(
                eventToPublish,
                new MessageMetadata(
                    CorrelationId: correlationId,
                    CausationId: eventToPublish.EventId.ToString(),
                    Source: "RabbitMqDecouplingIntegrationTests",
                    TenantId: null,
                    CreatedAtUtc: DateTime.UtcNow));

            await publisher.PublishAsync(envelope);

            var messageA = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var messageB = await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(20));

            Xunit.Assert.Equal(correlationId, messageA.Metadata.CorrelationId);
            Xunit.Assert.Equal(correlationId, messageB.Metadata.CorrelationId);
            Xunit.Assert.Equal(eventToPublish.AccountId, messageA.Event.AccountId);
            Xunit.Assert.Equal(eventToPublish.AccountId, messageB.Event.AccountId);
        }

    [Fact]
        public async Task Should_Move_Message_To_Dlq_When_Handler_Always_Fails()
        {
            var consumerName = $"dlq-test-{Guid.NewGuid():N}";
            var consumer = CreateBus(consumerName, retryCount: 1, retryDelaySeconds: 1);
            var publisher = CreateBus(consumerName: string.Empty, retryCount: 1, retryDelaySeconds: 1);

            await consumer.SubscribeAsync<TransactionCreatedEventV1>((_, _) =>
                Task.FromException(new InvalidOperationException("forced failure for DLQ scenario")));

            await Task.Delay(500);

            var @event = new TransactionCreatedEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                250m,
                "BRL",
                TransactionType.Credit);

            var envelope = new EventEnvelope<TransactionCreatedEventV1>(
                @event,
                new MessageMetadata(
                    CorrelationId: Guid.NewGuid().ToString(),
                    CausationId: @event.EventId.ToString(),
                    Source: "RabbitMqDecouplingIntegrationTests",
                    TenantId: null,
                    CreatedAtUtc: DateTime.UtcNow));

            await publisher.PublishAsync(envelope);

            var dlqQueue = $"{consumerName}.{nameof(TransactionCreatedEventV1)}.dlq";
            var dlqMessage = await WaitForMessageInQueueAsync(dlqQueue, TimeSpan.FromSeconds(30));

            Xunit.Assert.NotNull(dlqMessage);
        }

        private RabbitMqBus CreateBus(string consumerName, int retryCount, int retryDelaySeconds)
        {
            var options = Options.Create(new RabbitMqOptions
            {
                Host = _rabbitMqFixture.RabbitMqOptions.Host,
                Port = _rabbitMqFixture.RabbitMqOptions.Port,
                Username = _rabbitMqFixture.RabbitMqOptions.Username,
                Password = _rabbitMqFixture.RabbitMqOptions.Password,
                ConsumerName = consumerName,
                RetryCount = retryCount,
                RetryDelaySeconds = retryDelaySeconds
            });

            return new RabbitMqBus(options);
        }

        private async Task<BasicGetResult?> WaitForMessageInQueueAsync(string queueName, TimeSpan timeout)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqFixture.RabbitMqOptions.Host,
                Port = _rabbitMqFixture.RabbitMqOptions.Port,
                UserName = _rabbitMqFixture.RabbitMqOptions.Username,
                Password = _rabbitMqFixture.RabbitMqOptions.Password
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            var deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                var result = await channel.BasicGetAsync(queue: queueName, autoAck: true);
                if (result is not null)
                {
                    return result;
                }

                await Task.Delay(500);
            }

            return null;
        }
    }
}
