using Infrastructure.Test;

namespace E2E.Balance.Tests
{
    [CollectionDefinition("ServiceIndependenceInfrastructureCollection")]
    public class ServiceIndependenceCollectionDefinition
        : ICollectionFixture<BalanceCompleteInfrastructureFixture>
    {
    }
}
