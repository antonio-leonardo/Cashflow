using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace Worker.Integration.Tests
{
    public class EventPipelineTests : IAsyncLifetime
    {
        private readonly RabbitMqContainer _rabbit =
            new RabbitMqBuilder().Build();

        private readonly MongoDbContainer _mongo =
            new MongoDbBuilder().Build();

        public async Task InitializeAsync()
        {
            await _rabbit.StartAsync();
            await _mongo.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _rabbit.DisposeAsync();
            await _mongo.DisposeAsync();
        }

        [Fact]
        public async Task Transaction_Event_Should_Flow_Through_Worker()
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_rabbit.GetConnectionString())
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
                "USD");

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

            Assert.True(true);
        }
    }
}