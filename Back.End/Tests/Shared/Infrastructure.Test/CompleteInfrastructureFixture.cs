namespace Infrastructure.Test
{
    public class CompleteInfrastructureFixture : IAsyncLifetime
    {
        public PostgresContainerFixture PostgresContainerFixture { get; } = new();
        public MongoDbContainerFixture MongoDbContainerFixture { get; } = new();
        public RedisContainerFixture RedisContainerFixture { get; } = new();
        public RabbitMqContainerFixture RabbitMqContainerFixture { get; } = new();
        public OutboxWorkerFixture OutboxWorkerFixture { get; private set; }
        public BalanceWorkerFixture WorkerBalanceFixture { get; private set; }

        public CompleteInfrastructureFixture()
        {
            OutboxWorkerFixture = new(this);
            WorkerBalanceFixture = new(this);
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
            PostgresContainerFixture.InitializeAsync(),
            RedisContainerFixture.InitializeAsync(),
            RabbitMqContainerFixture.InitializeAsync());
            //MongoDbContainerFixture.InitializeAsync(),
            await Task.WhenAll(
            OutboxWorkerFixture.InitializeAsync(),
            WorkerBalanceFixture.InitializeAsync());
        }

        public async Task DisposeAsync()
        {
            await WorkerBalanceFixture.DisposeAsync();
            await OutboxWorkerFixture.DisposeAsync();
            await Task.WhenAll(
             RedisContainerFixture.DisposeAsync(),
             RabbitMqContainerFixture.DisposeAsync(),
             //MongoDbContainerFixture.DisposeAsync(),
             PostgresContainerFixture.DisposeAsync());
        }
    }
}