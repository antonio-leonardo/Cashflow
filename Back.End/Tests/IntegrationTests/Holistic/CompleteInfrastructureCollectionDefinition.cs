using Infrastructure.Test;

namespace Holistic.Integration.Tests
{
    [CollectionDefinition("CompleteInfrastructureCollection")]
    public class CompleteInfrastructureCollectionDefinition : ICollectionFixture<HolisticCompleteInfrastructureFixture>
    {
    }
}