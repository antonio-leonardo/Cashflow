using Infrastructure.Test;

namespace E2E.Report.Test
{
    [CollectionDefinition("CompleteInfrastructureCollection")]
    public class CompleteInfrastructureCollectionDefinition : ICollectionFixture<ReportCompleteInfrastructureFixture>
    {
    }
}