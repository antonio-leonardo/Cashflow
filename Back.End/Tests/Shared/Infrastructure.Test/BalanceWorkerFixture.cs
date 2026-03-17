using Cashflow.Shared.Messaging.RabbitMQ.DependecyInjection;
using Cashflow.Worker.Balance;
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
            _host = new HostBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IConnectionMultiplexer>(sp =>
                        ConnectionMultiplexer.Connect(_infra.RedisContainerFixture.ConnectionString));

                    services.AddScoped<RedisBalanceRepository>();
                    services.AddScoped<TransactionCreatedHandler>();

                    services.AddMessagingDependencyInjection(new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string>
                        {
                            ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                            ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                            ["RabbitMq:Username"] = "guest",
                            ["RabbitMq:Password"] = "guest",
                        })
                        .Build());

                    services.AddHostedService<Worker>();
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