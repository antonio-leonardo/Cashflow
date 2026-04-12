using Cashflow.Shared.Infrastructure.DependencyInjection;
using Cashflow.Shared.NoSql.Redis;
using Cashflow.Shared.Observability;

namespace Cashflow.Worker.Balance
{
    public class Program
    {
        protected Program() { }

        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddCashflowOpenTelemetryForWorker(builder.Configuration, "cashflow-balance-worker");

            builder.Services.AddCashflowMessaging(builder.Configuration);
            builder.Services.AddRedisProviderDependencyInjection(builder.Configuration);
            builder.Services.AddScoped<IBalanceProjectionRepository, RedisBalanceRepository>();
            builder.Services.AddScoped<TransactionCreatedHandler>();
            builder.Services.AddSingleton<
                Cashflow.Shared.Messaging.Abstractions.ITransactionEventProcessor<Cashflow.Service.Transaction.Domain.TransactionCreatedEventV1>,
                BalanceEventProcessor>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
