using Infrastructure.Test;

namespace Messaging.Integration.Tests
{
    [CollectionDefinition("RabbitMqCollection")]
    public class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqContainerFixture>
    {
    }
}