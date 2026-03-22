using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.Redis;
using Infrastructure.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Balance.Tests
{
    public class TransactionWebApplicationFactory : WebApplicationFactory<Cashflow.Service.Transaction.API.Program>
    {
        private readonly BalanceCompleteInfrastructureFixture _infra;
        private readonly bool _enableBalanceWorker;

        public TransactionWebApplicationFactory(
            BalanceCompleteInfrastructureFixture infra,
            bool enableBalanceWorker = false)
        {
            _infra = infra;
            _enableBalanceWorker = enableBalanceWorker;
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

                services.AddPostgresProviderDependencyInjection(configuration);
                services.AddDatabaseInfrastructureDependencyInjection(configuration);
                services.AddRabbitMQDependencyInjection(configuration);
                services.AddRedisProviderDependencyInjection(configuration);

                if (_enableBalanceWorker)
                {
                    services.AddScoped<Cashflow.Worker.Balance.RedisBalanceRepository>();
                    services.AddScoped<Cashflow.Worker.Balance.TransactionCreatedHandler>();
                    services.AddHostedService<Cashflow.Worker.Balance.Worker>();
                }
            });
        }
    }
}
