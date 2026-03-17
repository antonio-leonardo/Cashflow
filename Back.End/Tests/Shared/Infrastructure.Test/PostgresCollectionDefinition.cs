namespace Infrastructure.Test
{
    [CollectionDefinition("PostgresCollection")]
    public class PostgresCollectionDefinition : ICollectionFixture<PostgresContainerFixture>
    {
    }
}