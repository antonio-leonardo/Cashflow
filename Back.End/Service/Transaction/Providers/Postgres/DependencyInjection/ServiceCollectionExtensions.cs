using Cashflow.Back.End.Service.Transaction.Application.Commands;
using Cashflow.Back.End.Service.Transaction.Application.Queries;
using Cashflow.Back.End.Service.Transaction.Infrastructure.Logging;
using Cashflow.Back.End.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Back.End.Shared.Contracts.Idempotency;
using Cashflow.Back.End.Shared.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Back.End.Service.Transaction.Providers.Postgres.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlDatabaseDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("TransactionDb")
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