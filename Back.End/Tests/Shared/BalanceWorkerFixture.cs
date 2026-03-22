using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Test
{
    public class BalanceWorkerFixture : IAsyncLifetime
    {
        private readonly BaseCompleteInfrastructureFixture _infra;

        private IHost _host;

        public BalanceWorkerFixture(BalanceCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public BalanceWorkerFixture(HolisticCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task InitializeAsync()
        {
            _host = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString,
                        ["RabbitMq:ConsumerName"] = "balance",
                        ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                        ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                        ["RabbitMq:Username"] = "guest",
                        ["RabbitMq:Password"] = "guest",
                        ["Redis:Connection"] = _infra.RedisContainerFixture.ConnectionString
                    }!);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.AddRabbitMQDependencyInjection(configuration);
                    services.AddRedisProviderDependencyInjection(configuration);
                    services.AddScoped<Cashflow.Worker.Balance.RedisBalanceRepository>();
                    services.AddScoped<Cashflow.Worker.Balance.TransactionCreatedHandler>();
                    services.AddHostedService<Cashflow.Worker.Balance.Worker>();
                })
                .Build();

            await _host.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}