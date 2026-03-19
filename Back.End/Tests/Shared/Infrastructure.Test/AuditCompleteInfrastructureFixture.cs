namespace Infrastructure.Test
{
    public class AuditCompleteInfrastructureFixture : BaseCompleteInfrastructureFixture
    {
        public AuditWorkerFixture AuditWorkerFixture { get; private set; }

        public AuditCompleteInfrastructureFixture()
        {
            AuditWorkerFixture = new(this);
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await AuditWorkerFixture.DisposeAsync();
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await AuditWorkerFixture.InitializeAsync();
        }
    }
}