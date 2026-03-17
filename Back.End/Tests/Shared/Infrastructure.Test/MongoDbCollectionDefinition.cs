namespace Infrastructure.Test
{
    [CollectionDefinition("MongoDbCollection")]
    public class MongoDbCollectionDefinition : ICollectionFixture<MongoDbContainerFixture>
    {
    }
}