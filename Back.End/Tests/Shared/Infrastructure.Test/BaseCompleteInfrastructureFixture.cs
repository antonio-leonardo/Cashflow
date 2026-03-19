namespace Infrastructure.Test
{
    public class BaseCompleteInfrastructureFixture : IAsyncLifetime
    {
        public PostgresContainerFixture PostgresContainerFixture { get; } = new();
        public MongoDbContainerFixture MongoDbContainerFixture { get; } = new();
        public RabbitMqContainerFixture RabbitMqContainerFixture { get; } = new();
        public OutboxWorkerFixture OutboxWorkerFixture { get; private set; }
        public RedisContainerFixture RedisContainerFixture { get; } = new();
        public BaseCompleteInfrastructureFixture()
        {
            OutboxWorkerFixture = new(this);
        }

        public virtual async Task DisposeAsync()
        {
            await OutboxWorkerFixture.DisposeAsync();
            await Task.WhenAll(
             RedisContainerFixture.DisposeAsync(),
             RabbitMqContainerFixture.DisposeAsync(),
             PostgresContainerFixture.DisposeAsync(),
             MongoDbContainerFixture.DisposeAsync());
        }

        public virtual async Task InitializeAsync()
        {
            await Task.WhenAll(
            PostgresContainerFixture.InitializeAsync(),
            RedisContainerFixture.InitializeAsync(),
            RabbitMqContainerFixture.InitializeAsync(),
            MongoDbContainerFixture.InitializeAsync());
            await Task.WhenAll(
            OutboxWorkerFixture.InitializeAsync());
        }
    }
}