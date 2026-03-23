using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Testcontainers.RabbitMq;

namespace Infrastructure.Test
{
    public class RabbitMqContainerV1Fixture : IAsyncLifetime
    {
        public RabbitMqOptions RabbitMqOptions = new()
        {
            Username = "guest",
            Password = "guest"
        };

        public RabbitMqContainer RabbitMq { get; }

        public RabbitMqContainerV1Fixture()
        {
            RabbitMq = new RabbitMqBuilder("rabbitmq:3-management")
                .WithUsername(RabbitMqOptions.Username)
                .WithPassword(RabbitMqOptions.Password)
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

        public string NetworkHost => RabbitMq.Hostname;
        public int NetworkPort => 5672;
    }
}
