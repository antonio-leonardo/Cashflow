using Infrastructure.Test;

namespace Storage.Integration.Tests
{
    [CollectionDefinition("AzuriteCollection")]
    public class AzuriteCollectionDefinition : ICollectionFixture<AzuriteContainerFixture> { }
}
