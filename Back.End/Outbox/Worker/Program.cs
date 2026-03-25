using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;

namespace Cashflow.Outbox.Worker
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddPostgresProviderDependencyInjection(builder.Configuration);
            builder.Services.AddDatabaseInfrastructureDependencyInjection(builder.Configuration);
            builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}