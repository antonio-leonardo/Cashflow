using Cashflow.Service.Transaction.Application.Commands;
using Cashflow.Service.Transaction.Application.Queries;
using Cashflow.Service.Transaction.Infrastructure.Logging;
using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Shared.Contracts.Idempotency;
using Cashflow.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Service.Transaction.Postgres.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseInfrastructureDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IProcessedEventStore, ProcessedEventStore>();
            services.AddScoped<ILogService, ConsoleLogService>();
            services.AddScoped<ICreateTransactionHandler, CreateTransactionHandler>();
            services.AddScoped<IGetTransactionQueryHandler, GetTransactionQueryHandler>();

            return services;
        }
    }
}