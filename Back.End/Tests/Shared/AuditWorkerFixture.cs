using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Infrastructure.Test
{
    public class AuditWorkerFixture : IAsyncLifetime
    {
        private readonly AuditCompleteInfrastructureFixture _infra;

        private IHost _host;

        public AuditWorkerFixture(AuditCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        public async Task InitializeAsync()
        {
            _host = new HostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddMessagingDependencyInjection(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString,
                    ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                    ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                    ["RabbitMq:Username"] = "guest",
                    ["RabbitMq:Password"] = "guest",
                    ["Mongo:Connection"] = _infra.MongoDbContainerFixture.ConnectionString
                })
                .Build());

                services.AddSingleton<IMongoClient>(sp =>
                {
                    return new MongoClient(_infra.MongoDbContainerFixture.ConnectionString);
                });

                services.AddScoped(sp =>
                {
                    var client = sp.GetRequiredService<IMongoClient>();
                    return client.GetDatabase("cashflow-audit");
                });

                services.AddScoped<Cashflow.Worker.Audit.TransactionCreatedHandler>();
                services.AddHostedService<Cashflow.Worker.Audit.Worker>();
            })
            .Build();

            await _host.StartAsync();
        }
    }
}