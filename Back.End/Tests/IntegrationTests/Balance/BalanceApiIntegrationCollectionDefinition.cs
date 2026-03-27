using Infrastructure.Test;

namespace Balance.Integration.Tests
{
    [CollectionDefinition("BalanceApiIntegrationCollection")]
    public sealed class BalanceApiIntegrationCollectionDefinition
        : ICollectionFixture<BalanceApiIntegrationInfrastructureFixture>
    {
    }
}
