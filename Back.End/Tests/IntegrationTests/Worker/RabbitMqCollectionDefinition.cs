using Infrastructure.Test;

namespace Worker.Integration.Tests
{
    [CollectionDefinition("RabbitMqCollection")]
    public class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqContainerV1Fixture>
    {
    }
}