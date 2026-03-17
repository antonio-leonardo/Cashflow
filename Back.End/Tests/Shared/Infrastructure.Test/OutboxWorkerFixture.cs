using Cashflow.Back.End.Service.Transaction.Providers.Postgres.DependencyInjection;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ.DependecyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Test
{
    public class OutboxWorkerFixture : IAsyncLifetime
    {
        private readonly CompleteInfrastructureFixture _infra;

        private IHost _host;

        public OutboxWorkerFixture(CompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task InitializeAsync()
        {
            var builder = Host.CreateApplicationBuilder();

            var config = new Dictionary<string, string>
            {
                ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString,
                ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                ["RabbitMq:Username"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Username,
                ["RabbitMq:Password"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Password,
                ["Redis:Connection"] = _infra.RedisContainerFixture.ConnectionString
            };

            builder.Configuration.AddInMemoryCollection(config);

            builder.Services.AddSqlDatabaseDependencyInjection(builder.Configuration);
            builder.Services.AddMessagingDependencyInjection(builder.Configuration);
            builder.Services.AddSingleton<IMessageBus, Cashflow.Back.End.Outbox.Worker.ConsoleMessageBus>();
            builder.Services.AddHostedService<Cashflow.Back.End.Outbox.Worker.Worker>();

            _host = builder.Build();

            await _host.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_host != null)
                await _host.StopAsync();
        }
    }
}