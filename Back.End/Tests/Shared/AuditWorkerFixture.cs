using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Infrastructure.Test
{
    public class AuditWorkerFixture : IAsyncLifetime
    {
        private readonly BaseCompleteInfrastructureFixture _infra;

        private IHost? _host;

        public AuditWorkerFixture(AuditCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public AuditWorkerFixture(HolisticCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public async Task DisposeAsync()
            => await StopAsync();

        public async Task StartAsync()
        {
            if (_host is not null)
            {
                return;
            }

            InitializeHost();
            var host = _host ?? throw new InvalidOperationException("Audit worker host nao foi inicializado.");
            await host.StartAsync();
        }

        public async Task InitializeAsync()
            => await StartAsync();

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

        private void InitializeHost()
        {
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // já registrado por outro fixture, ignorar
            }
            _host = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString,
                        ["RabbitMq:ConsumerName"] = "audit",
                        ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                        ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                        ["RabbitMq:Username"] = "guest",
                        ["RabbitMq:Password"] = "guest",
                        ["Mongo:Connection"] = _infra.MongoDbContainerFixture.ConnectionString
                    }!);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.AddRabbitMQDependencyInjection(configuration);
                    services.AddMongoDBProviderDependencyInjection(configuration, "cashflow-audit");
                    services.AddScoped<Cashflow.Worker.Audit.IAuditRepository, Cashflow.Worker.Audit.MongoAuditRepository>();
                    services.AddScoped<Cashflow.Worker.Audit.TransactionCreatedHandler>();
                    services.AddSingleton<
                        Cashflow.Shared.Messaging.Abstractions.ITransactionEventProcessor<Cashflow.Service.Transaction.Domain.TransactionCreatedEventV1>,
                        Cashflow.Worker.Audit.AuditEventProcessor>();
                    services.AddHostedService<Cashflow.Worker.Audit.Worker>();
                })
                .Build();
        }
    }
}
