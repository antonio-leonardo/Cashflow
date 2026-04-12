using Infrastructure.Test;

namespace Messaging.Integration.Tests
{
    [CollectionDefinition("CosmosDbEmulatorCollection")]
    public class CosmosDbEmulatorCollectionDefinition
        : ICollectionFixture<CosmosDbEmulatorFixture> { }
}
