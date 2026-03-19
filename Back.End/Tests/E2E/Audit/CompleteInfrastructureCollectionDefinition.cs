using Infrastructure.Test;

namespace E2E.Audit.Test
{
    [CollectionDefinition("CompleteInfrastructureCollection")]
    public class CompleteInfrastructureCollectionDefinition : ICollectionFixture<AuditCompleteInfrastructureFixture>
    {
    }
}