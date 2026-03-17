using Infrastructure.Test;

namespace E2E.Tests
{
    [CollectionDefinition("CompleteInfrastructureCollection")]
    public class CompleteInfrastructureCollectionDefinition : ICollectionFixture<CompleteInfrastructureFixture>
    {
    }
}