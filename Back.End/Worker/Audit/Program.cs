using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Shared.Observability;

namespace Cashflow.Worker.Audit
{
    public class Program
    {
        protected Program() { }

        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddCashflowOpenTelemetryForWorker(builder.Configuration, "cashflow-audit-worker");

            builder.Services.AddCashflowMessaging(builder.Configuration);

            builder.Services.AddMongoDBProviderDependencyInjection(builder.Configuration, "cashflow-audit");
            builder.Services.AddScoped<IAuditRepository, MongoAuditRepository>();
            builder.Services.AddScoped<TransactionCreatedHandler>();
            builder.Services.AddSingleton<
                Cashflow.Shared.Messaging.Abstractions.ITransactionEventProcessor<Cashflow.Service.Transaction.Domain.TransactionCreatedEventV1>,
                AuditEventProcessor>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
