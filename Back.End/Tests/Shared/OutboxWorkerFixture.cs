using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Test
{
    public class OutboxWorkerFixture : IAsyncLifetime
    {
        private readonly BaseCompleteInfrastructureFixture _infra;

        private IHost? _host;

        public OutboxWorkerFixture(BaseCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task InitializeAsync()
            => await StartAsync();

        public async Task StartAsync()
        {
            if (_host is not null)
            {
                return;
            }

            var builder = Host.CreateApplicationBuilder();

            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString,
                ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                ["RabbitMq:Username"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Username,
                ["RabbitMq:Password"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Password,
                ["Redis:Connection"] = _infra.RedisContainerFixture.ConnectionString,
                ["Mongo:Connection"] = _infra.MongoDbContainerFixture.ConnectionString
            };

            builder.Configuration.AddInMemoryCollection(config);

            builder.Services.AddPostgresProviderDependencyInjection(builder.Configuration);
            builder.Services.AddDatabaseInfrastructureDependencyInjection(builder.Configuration);
            builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);
            builder.Services.AddHostedService<Cashflow.Outbox.Worker.Worker>();

            _host = builder.Build();

            await _host.StartAsync();
        }

        public async Task StopAsync()
        {
            if (_host is null)
            {
                return;
            }

            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }

        public async Task DisposeAsync()
        {
            await StopAsync();
        }
    }
}
