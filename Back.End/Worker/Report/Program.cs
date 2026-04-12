using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Shared.Observability;

namespace Cashflow.Worker.Report
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddCashflowOpenTelemetryForWorker(builder.Configuration, "cashflow-report-worker");

            builder.Services.AddCashflowMessaging(builder.Configuration);

            builder.Services.AddCashflowStorage(builder.Configuration);
            builder.Services.AddMongoDBProviderDependencyInjection(builder.Configuration, "cashflow-report");
            builder.Services.AddScoped<IReportRepository, MongoReportRepository>();
            builder.Services.AddScoped<TransactionCreatedHandler>();
            builder.Services.AddScoped<ReportExportService>();
            builder.Services.AddSingleton<
                Cashflow.Shared.Messaging.Abstractions.ITransactionEventProcessor<Cashflow.Service.Transaction.Domain.TransactionCreatedEventV1>,
                ReportEventProcessor>();

            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
