namespace Infrastructure.Test
{
    public class HolisticCompleteInfrastructureFixture : BaseCompleteInfrastructureFixture
    {
        protected override bool InitializeOutboxWorkerWithBase => false;
        public HolisticInfrastructureFixture AllInfrastructureFixture { get; private set; }

        public KeycloakContainerFixture KeycloakFixture = new();

        public TransactionApiContainerFixture TransactionApiFixture { get; private set; } = default!;
        public BalanceQueryApiContainerFixture BalanceQueryApiFixture { get; private set; } = default!;

        public HolisticCompleteInfrastructureFixture()
        {
            AllInfrastructureFixture = new(this);
        }

        public override async Task DisposeAsync()
        {
            await OutboxWorkerFixture.DisposeAsync();
            await TransactionApiFixture.DisposeAsync();
            await BalanceQueryApiFixture.DisposeAsync();
            await Task.WhenAll(
                AllInfrastructureFixture.DisposeAsync(),
                KeycloakFixture.DisposeAsync());
            await base.DisposeAsync();
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await Task.WhenAll(
                AllInfrastructureFixture.InitializeAsync(),
                KeycloakFixture.InitializeAsync());
            TransactionApiFixture = new TransactionApiContainerFixture(this);
            BalanceQueryApiFixture = new BalanceQueryApiContainerFixture(this);
            await TransactionApiFixture.InitializeAsync();
            await BalanceQueryApiFixture.InitializeAsync();
            await OutboxWorkerFixture.InitializeAsync();
        }
    }
}
