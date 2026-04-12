using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Shared.Observability;
using Cashflow.Worker.Report;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        var configuration = ctx.Configuration;

        services.AddCashflowOpenTelemetryForWorker(configuration, "cashflow-report-function");
        services.AddCashflowStorage(configuration);
        services.AddMongoDBProviderDependencyInjection(configuration, "cashflow-report");
        services.AddScoped<IReportRepository, MongoReportRepository>();
        services.AddScoped<TransactionCreatedHandler>();
        services.AddScoped<ReportExportService>();
        services.AddSingleton<
            ITransactionEventProcessor<TransactionCreatedEventV1>,
            ReportEventProcessor>();
    })
    .Build();

await host.RunAsync();
