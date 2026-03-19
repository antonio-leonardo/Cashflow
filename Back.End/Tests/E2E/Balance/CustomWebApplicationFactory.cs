using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependecyInjection;
using Infrastructure.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace E2E.Balance.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly BalanceCompleteInfrastructureFixture _infra;

        public CustomWebApplicationFactory(BalanceCompleteInfrastructureFixture infra)
        {
            _infra = infra;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string>
                {
                    ["RabbitMq:Host"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Host,
                    ["RabbitMq:Port"] = _infra.RabbitMqContainerFixture.RabbitMqOptions.Port.ToString(),
                    ["RabbitMq:Username"] = "guest",
                    ["RabbitMq:Password"] = "guest",

                    ["Redis:Connection"] = _infra.RedisContainerFixture.ConnectionString,
                    ["ConnectionStrings:Postgres"] = _infra.PostgresContainerFixture.ConnectionString
                };
                config.AddInMemoryCollection(settings!);
            });

            builder.ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                services.AddSqlDatabaseDependencyInjection(configuration);
                services.AddMessagingDependencyInjection(configuration);

                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    return ConnectionMultiplexer.Connect(_infra.RedisContainerFixture.ConnectionString);
                });

                services.AddScoped<Cashflow.Worker.Balance.RedisBalanceRepository>();
                services.AddScoped<Cashflow.Worker.Balance.TransactionCreatedHandler>();

                services.AddHostedService<Cashflow.Outbox.Worker.Worker>();
                services.AddHostedService<Cashflow.Worker.Balance.Worker>();
            });
        }
    }
}