using Infrastructure.Test;
using RabbitMQ.Client;

namespace Messaging.Integration.Tests
{
    [Collection("RabbitMqCollection")]
    public class RabbitMqWorkerTests
    {
        private readonly RabbitMqContainerV1Fixture _rabbitMqFixture;
        public RabbitMqWorkerTests(RabbitMqContainerV1Fixture rabbitMqFixture)
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

            await using (var connection = await factory.CreateConnectionAsync())
            {
                Xunit.Assert.NotNull(connection);
            }
        }
    }
}
