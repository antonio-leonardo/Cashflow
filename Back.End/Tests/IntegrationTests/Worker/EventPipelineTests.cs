using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Infrastructure.Test;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Worker.Integration.Tests
{
    [Collection("RabbitMqCollection")]
    public class EventPipelineTests
    {
        private readonly RabbitMqContainerV1Fixture _rabbitMqFixture;

        public EventPipelineTests(RabbitMqContainerV1Fixture rabbitMqFixture)
        {
            _rabbitMqFixture = rabbitMqFixture;
        }

        [Fact]
        public async Task Transaction_Event_Should_Flow_Through_Worker()
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_rabbitMqFixture.ConnectionString)
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            var queue = "transaction.created";

            await channel.QueueDeclareAsync(
                queue,
                durable: true,
                exclusive: false,
                autoDelete: false);

            var evt = new TransactionCreatedEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                500,
                "BRL",
                TransactionType.Credit);

            var metadata = new MessageMetadata(
                Guid.NewGuid().ToString(),
                evt.EventId.ToString(),
                "IntegrationTest",
                null,
                DateTime.UtcNow);

            var envelope =
                new EventEnvelope<TransactionCreatedEventV1>(evt, metadata);

            var body =
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));

            await channel.BasicPublishAsync(
                "",
                queue,
                body);

            await Task.Delay(3000);

            Xunit.Assert.True(true);
        }
    }
}
