using Infrastructure.Test;
using RabbitMQ.Client;

namespace Messaging.Integration.Tests
{
    [Collection("RabbitMqCollection")]
    public class RabbitMqWorkerTests
    {
        private readonly RabbitMqContainerFixture _rabbitMqFixture;
        public RabbitMqWorkerTests(RabbitMqContainerFixture rabbitMqFixture)
        {
            _rabbitMqFixture = rabbitMqFixture;
        }

        [Fact]
        public async Task Should_Publish_Message()
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_rabbitMqFixture.ConnectionString)
            };

            await using var connection =
                await factory.CreateConnectionAsync();

            Assert.NotNull(connection);
        }
    }
}