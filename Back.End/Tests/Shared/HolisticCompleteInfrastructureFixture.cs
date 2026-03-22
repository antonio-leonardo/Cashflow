namespace Infrastructure.Test
{
    public class HolisticCompleteInfrastructureFixture : BaseCompleteInfrastructureFixture
    {
        protected override bool InitializeOutboxWorkerWithBase => false;
        public HolisticInfrastructureFixture AllInfrastructureFixture { get; private set; }

        public KeycloakContainerFixture KeycloakFixture = new();

        public TransactionApiContainerFixture TransactionApiFixture { get; private set; } = default!;

        public HolisticCompleteInfrastructureFixture()
        {
            AllInfrastructureFixture = new(this);
        }

        public override async Task DisposeAsync()
        {
            await OutboxWorkerFixture.DisposeAsync();
            await TransactionApiFixture.DisposeAsync();
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
            await TransactionApiFixture.InitializeAsync();
            await OutboxWorkerFixture.InitializeAsync();
        }
    }
}