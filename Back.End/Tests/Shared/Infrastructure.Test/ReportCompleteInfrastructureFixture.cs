namespace Infrastructure.Test
{
    public class ReportCompleteInfrastructureFixture : BaseCompleteInfrastructureFixture
    {
        public ReportWorkerFixture ReportWorkerFixture { get; private set; }
        public ReportCompleteInfrastructureFixture()
        {
            ReportWorkerFixture = new(this);
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await ReportWorkerFixture.DisposeAsync();
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await ReportWorkerFixture.InitializeAsync();
        }
    }
}