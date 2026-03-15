using Cashflow.Back.End.Service.Transaction.Application.Commands;
using Cashflow.Back.End.Service.Transaction.Application.Queries;
using Cashflow.Back.End.Service.Transaction.Infrastructure.Logging;
using Cashflow.Back.End.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Back.End.Shared.Contracts.Idempotency;
using Cashflow.Back.End.Shared.Logging;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Back.End.Service.Transaction.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDependencyInjection(
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

            //Provider de mensageria: RabbitMQ
            //services.AddOptions<RabbitMqOptions>().;
            services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
            services.AddSingleton<IMessageBus, RabbitMqBus>();

            return services;
        }
    }
}