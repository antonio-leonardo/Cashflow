using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Infrastructure.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace E2E.Report.Test
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly ReportCompleteInfrastructureFixture _infra;

        public CustomWebApplicationFactory(ReportCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            BsonSerializer.RegisterSerializer(
                new GuidSerializer(GuidRepresentation.Standard)
            );
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders(); // remove EventLog, Console, etc.
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

                services.AddHostedService<Cashflow.Outbox.Worker.Worker>();
                services.AddHostedService<Cashflow.Worker.Audit.Worker>();
            });
        }
    }
}