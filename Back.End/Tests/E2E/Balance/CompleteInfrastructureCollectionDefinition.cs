using Infrastructure.Test;

namespace E2E.Balance.Tests
{
    [CollectionDefinition("CompleteInfrastructureCollection")]
    public class CompleteInfrastructureCollectionDefinition : ICollectionFixture<BalanceCompleteInfrastructureFixture>
    {
    }
}