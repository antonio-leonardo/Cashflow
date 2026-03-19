namespace Infrastructure.Test
{
    public class BalanceCompleteInfrastructureFixture : BaseCompleteInfrastructureFixture
    {
        public BalanceWorkerFixture WorkerBalanceFixture { get; private set; }

        public BalanceCompleteInfrastructureFixture()
        {
            WorkerBalanceFixture = new(this);
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await WorkerBalanceFixture.InitializeAsync();
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await WorkerBalanceFixture.DisposeAsync();
        }
    }
}