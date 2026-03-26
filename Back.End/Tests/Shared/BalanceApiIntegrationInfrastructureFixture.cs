namespace Infrastructure.Test
{
    public sealed class BalanceApiIntegrationInfrastructureFixture : BaseCompleteInfrastructureFixture
    {
        protected override bool InitializeOutboxWorkerWithBase => false;

        public BalanceQueryApiContainerFixture BalanceQueryApiFixture { get; private set; } = default!;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            BalanceQueryApiFixture = new BalanceQueryApiContainerFixture(this);
            await BalanceQueryApiFixture.InitializeAsync();
        }

        public override async Task DisposeAsync()
        {
            if (BalanceQueryApiFixture is not null)
            {
                await BalanceQueryApiFixture.DisposeAsync();
            }

            await base.DisposeAsync();
        }
    }
}
