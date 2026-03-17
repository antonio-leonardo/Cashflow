namespace Infrastructure.Test
{
    [CollectionDefinition("RedisCollection")]
    public class RedisCollectionDefinition : ICollectionFixture<RedisContainerFixture>
    {
    }
}