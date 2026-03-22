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
    public class ReportWorkerFixture : IAsyncLifetime
    {
        private readonly BaseCompleteInfrastructureFixture _infra;

        private IHost _host;

        public ReportWorkerFixture(ReportCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        public ReportWorkerFixture(HolisticCompleteInfrastructureFixture infra)
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
                        ["RabbitMq:ConsumerName"] = "report",
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
                    services.AddMongoDBProviderDependencyInjection(configuration, "cashflow-report");
                    services.AddScoped<Cashflow.Worker.Report.TransactionCreatedHandler>();
                    services.AddHostedService<Cashflow.Worker.Report.Worker>();
                })
                .Build();

            await _host.StartAsync();
        }
    }
}