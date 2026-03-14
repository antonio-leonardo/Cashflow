using Cashflow.Shared.Contracts.Idempotency;
using Cashflow.Shared.Logging;
using Cashflow.Transaction.Infrastructure.Logging;
using Cashflow.Transaction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Transaction.Application.Commands;
using Transaction.Application.Queries;

namespace Cashflow.Transaction.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTransactionInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("TransactionDb")
                ?? "Host=localhost;Port=5432;Database=cashflow;Username=admin;Password=admin";

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