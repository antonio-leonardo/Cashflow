using Infrastructure.Test;

namespace E2E.Report.Test
{
    [CollectionDefinition("ServiceIndependenceInfrastructureCollection")]
    public class ServiceIndependenceCollectionDefinition
        : ICollectionFixture<ReportCompleteInfrastructureFixture>
    {
    }
}
