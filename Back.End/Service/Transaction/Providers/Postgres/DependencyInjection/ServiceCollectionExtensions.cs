using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Service.Transaction.Postgres.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPostgresProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "Connection string not configured.");

            services.AddDbContext<TransactionDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);

                    npgsqlOptions.CommandTimeout(10);
                }));

            services.AddDbContext<IdempotencyDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);

                    npgsqlOptions.CommandTimeout(10);
                }));

            return services;
        }
    }
}