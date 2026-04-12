using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;
using Infrastructure.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace E2E.Report.Test
{
    public class TransactionWebApplicationFactory : WebApplicationFactory<Cashflow.Service.Transaction.API.Program>
    {
        private readonly ReportCompleteInfrastructureFixture _infra;
        private readonly bool _enableReportWorker;

        public TransactionWebApplicationFactory(
            ReportCompleteInfrastructureFixture infra,
            bool enableReportWorker = false)
        {
            _infra = infra;
            _enableReportWorker = enableReportWorker;
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // já registrado por outro fixture, ignorar
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string>
                {
                    ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                    ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                    ["RabbitMq:Username"] = "guest",
                    ["RabbitMq:Password"] = "guest",

                    ["Mongo:Connection"] = _infra.MongoDbContainerFixture.ConnectionString,
                    ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString
                };
                config.AddInMemoryCollection(settings!);
            });

            builder.ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                services.AddPostgresProviderDependencyInjection(configuration);
                services.AddDatabaseInfrastructureDependencyInjection(configuration);
                services.AddRabbitMQDependencyInjection(configuration);
                services.AddMongoDBProviderDependencyInjection(configuration, "cashflow-report");

                if (_enableReportWorker)
                {
                    services.AddScoped<Cashflow.Worker.Report.IReportRepository, Cashflow.Worker.Report.MongoReportRepository>();
                    services.AddScoped<Cashflow.Worker.Report.TransactionCreatedHandler>();
                    services.AddSingleton<
                        Cashflow.Shared.Messaging.Abstractions.ITransactionEventProcessor<Cashflow.Service.Transaction.Domain.TransactionCreatedEventV1>,
                        Cashflow.Worker.Report.ReportEventProcessor>();
                    services.AddHostedService<Cashflow.Worker.Report.Worker>();
                }
            });
        }
    }
}
