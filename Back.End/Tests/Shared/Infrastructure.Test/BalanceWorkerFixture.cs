using Cashflow.Shared.Messaging.RabbitMQ.DependecyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Infrastructure.Test
{
    public class BalanceWorkerFixture : IAsyncLifetime
    {
        private readonly CompleteInfrastructureFixture _infra;

        private IHost _host;

        public BalanceWorkerFixture(CompleteInfrastructureFixture infra)
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

            builder.Services.AddMessagingDependencyInjection(builder.Configuration);
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connection = config["Redis:Connection"];

                return ConnectionMultiplexer.Connect(connection);
            });
            builder.Services.AddScoped<Cashflow.Worker.Balance.TransactionCreatedHandler>();
            builder.Services.AddHostedService<Cashflow.Worker.Balance.Worker>();

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