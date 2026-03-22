using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Infrastructure.Test
{
    public class BaseCompleteInfrastructureFixture : IAsyncLifetime
    {
        protected virtual bool InitializeOutboxWorkerWithBase => true;
        public INetwork Network { get; } = new NetworkBuilder()
            .WithName($"cashflow-test-{Guid.NewGuid()}")
            .Build();

        public const string PostgresAlias = "postgres";
        public const string MongoAlias = "mongo";
        public const string RabbitMqAlias = "rabbitmq";
        public const string RedisAlias = "redis";

        public PostgresContainerFixture PostgresContainerFixture { get; }
        public MongoDbContainerFixture MongoDbContainerFixture { get; }
        public RabbitMqContainerV2Fixture RabbitMqContainerFixture { get; }
        public RedisContainerFixture RedisContainerFixture { get; }
        public OutboxWorkerFixture OutboxWorkerFixture { get; private set; }

        public BaseCompleteInfrastructureFixture()
        {
            PostgresContainerFixture = new(Network, PostgresAlias);
            MongoDbContainerFixture = new(Network, MongoAlias);
            RabbitMqContainerFixture = new(Network, RabbitMqAlias);
            RedisContainerFixture = new(Network, RedisAlias);
            OutboxWorkerFixture = new(this);
        }

        public virtual async Task InitializeAsync()
        {
            await Network.CreateAsync();
            await Task.WhenAll(
                PostgresContainerFixture.InitializeAsync(),
                RedisContainerFixture.InitializeAsync(),
                RabbitMqContainerFixture.InitializeAsync(),
                MongoDbContainerFixture.InitializeAsync());
            if (InitializeOutboxWorkerWithBase)
                await OutboxWorkerFixture.InitializeAsync();
        }

        public virtual async Task DisposeAsync()
        {
            if (InitializeOutboxWorkerWithBase)
                await OutboxWorkerFixture.DisposeAsync();

            await Task.WhenAll(
                RedisContainerFixture.DisposeAsync(),
                RabbitMqContainerFixture.DisposeAsync(),
                PostgresContainerFixture.DisposeAsync(),
                MongoDbContainerFixture.DisposeAsync());
            await Network.DisposeAsync();
        }
    }
}