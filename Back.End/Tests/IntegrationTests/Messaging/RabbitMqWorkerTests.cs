using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Messaging.Integration.Tests
{
    public class RabbitMqWorkerTests
    {
        [Fact]
        public async Task Worker_Should_Consume_Transaction_Event()
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost"
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            var queue = "transaction.created";

            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false);

            var evt = new TransactionCreatedEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                100,
                "USD");

            var metadata = new MessageMetadata(
                Guid.NewGuid().ToString(),
                evt.EventId.ToString(),
                "IntegrationTest",
                null,
                DateTime.UtcNow);

            var envelope = new EventEnvelope<TransactionCreatedEventV1>(
                evt,
                metadata);

            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(envelope));

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queue,
                body: body);

            var consumed = false;

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                consumed = true;
                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(
                queue: queue,
                autoAck: true,
                consumer: consumer);

            await Task.Delay(2000);

            Assert.True(consumed);
        }
    }
}