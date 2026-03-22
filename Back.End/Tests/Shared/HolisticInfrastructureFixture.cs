namespace Infrastructure.Test
{
    public class HolisticInfrastructureFixture : IAsyncLifetime
    {
        private readonly BaseCompleteInfrastructureFixture _infra;

        private readonly ReportWorkerFixture _reportWorkerFixture;
        private readonly AuditWorkerFixture _auditWorkerFixture;
        private readonly BalanceWorkerFixture _balanceWorkerFixture;

        public HolisticInfrastructureFixture(HolisticCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _reportWorkerFixture = new(infra);
            _auditWorkerFixture = new(infra);
            _balanceWorkerFixture = new(infra);
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _reportWorkerFixture.InitializeAsync(),
                _auditWorkerFixture.InitializeAsync(),
                _balanceWorkerFixture.InitializeAsync());
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                _reportWorkerFixture.DisposeAsync(),
                _auditWorkerFixture.DisposeAsync(),
                _balanceWorkerFixture.DisposeAsync());
        }
    }
}