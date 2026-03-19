using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using System.Text.Json;
using Testcontainers.RabbitMq;

namespace Infrastructure.Test
{
    public class RabbitMqContainerFixture : IAsyncLifetime
    {
        public RabbitMqOptions RabbitMqOptions = new()
        {
            //Host = "localhost",
            //Port = 5672,
            Username = "guest",
            Password = "guest"
        };

        public RabbitMqContainer RabbitMq { get; }

        public RabbitMqContainerFixture()
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

        public async Task DisposeAsync()
        {
            await RabbitMq.DisposeAsync();
        }

        public string ConnectionString =>
            $"amqp://{RabbitMqOptions.Username}:{RabbitMqOptions.Password}@{RabbitMq.Hostname}:{RabbitMq.GetMappedPublicPort(5672)}";

        public string Section => JsonSerializer.Serialize(RabbitMqOptions);
    }
}