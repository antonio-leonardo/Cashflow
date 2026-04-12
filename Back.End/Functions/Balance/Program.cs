using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.NoSql.Redis;
using Cashflow.Shared.Observability;
using Cashflow.Worker.Balance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        var configuration = ctx.Configuration;

        services.AddCashflowOpenTelemetryForWorker(configuration, "cashflow-balance-function");
        services.AddRedisProviderDependencyInjection(configuration);
        services.AddScoped<IBalanceProjectionRepository, RedisBalanceRepository>();
        services.AddScoped<TransactionCreatedHandler>();
        services.AddSingleton<
            ITransactionEventProcessor<TransactionCreatedEventV1>,
            BalanceEventProcessor>();
    })
    .Build();

await host.RunAsync();
