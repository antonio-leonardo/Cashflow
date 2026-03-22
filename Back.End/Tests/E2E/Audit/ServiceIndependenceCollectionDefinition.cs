using Infrastructure.Test;

namespace E2E.Audit.Test
{
    [CollectionDefinition("ServiceIndependenceInfrastructureCollection")]
    public class ServiceIndependenceCollectionDefinition
        : ICollectionFixture<AuditCompleteInfrastructureFixture>
    {
    }
}
