using Cashflow.Service.Transaction.Application.Commands;
using Cashflow.Service.Transaction.Application.Queries;
using Cashflow.Service.Transaction.Infrastructure.Logging;
using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Shared.Contracts.Idempotency;
using Cashflow.Shared.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Service.Transaction.Postgres.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlDatabaseDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "Connection string 'TransactionDb' not configured.");

            services.AddDbContext<TransactionDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddDbContext<IdempotencyDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IProcessedEventStore, ProcessedEventStore>();

            services.AddScoped<ILogService, ConsoleLogService>();

            services.AddScoped<ICreateTransactionHandler, CreateTransactionHandler>();
            services.AddScoped<IGetTransactionQueryHandler, GetTransactionQueryHandler>();

            return services;
        }
    }
}