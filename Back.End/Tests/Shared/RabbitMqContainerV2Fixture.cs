using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using DotNet.Testcontainers.Networks;
using Testcontainers.RabbitMq;

namespace Infrastructure.Test
{
    public class RabbitMqContainerV2Fixture : IAsyncLifetime
    {
        private readonly string _alias;

        public RabbitMqOptions RabbitMqOptions = new()
        {
            Username = "guest",
            Password = "guest"
        };

        public RabbitMqContainer RabbitMq { get; }

        public RabbitMqContainerV2Fixture(INetwork network, string alias)
        {
            _alias = alias;
            RabbitMq = new RabbitMqBuilder("rabbitmq:3-management")
                .WithUsername(RabbitMqOptions.Username)
                .WithPassword(RabbitMqOptions.Password)
                .WithNetwork(network)
                .WithNetworkAliases(alias)
                .Build();
            RabbitMqOptions.Host = RabbitMq.Hostname;
        }

        public async Task InitializeAsync()
        {
            await RabbitMq.StartAsync();
            RabbitMqOptions.Port = RabbitMq.GetMappedPublicPort(5672);
        }

        public async Task DisposeAsync() => await RabbitMq.DisposeAsync();

        public string ConnectionString =>
            $"amqp://{RabbitMqOptions.Username}:{RabbitMqOptions.Password}@{RabbitMq.Hostname}:{RabbitMq.GetMappedPublicPort(5672)}";

        public string NetworkHost => _alias;
        public int NetworkPort => 5672;
    }
}