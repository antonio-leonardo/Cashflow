using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.Observability;

namespace Cashflow.Outbox.Worker
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddCashflowOpenTelemetryForWorker(builder.Configuration, "cashflow-outbox-worker");
            builder.Services.AddPostgresProviderDependencyInjection(builder.Configuration);
            builder.Services.AddDatabaseInfrastructureDependencyInjection(builder.Configuration);
            builder.Services.AddCashflowMessaging(builder.Configuration);
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
