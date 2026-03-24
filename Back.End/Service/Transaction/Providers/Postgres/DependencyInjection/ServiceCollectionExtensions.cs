using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cashflow.Service.Transaction.Postgres.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPostgresProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = ResolveConnectionString(configuration)
                ?? throw new InvalidOperationException("Postgres connection not configured.");

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

        private static string? ResolveConnectionString(IConfiguration configuration)
        {
            var direct = configuration.GetConnectionString("Postgres");
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            var section = configuration.GetSection("Infrastructure:Postgres");
            if (!section.Exists())
            {
                section = configuration.GetSection("Postgres");
            }

            if (!section.Exists())
            {
                return null;
            }

            var host = section["Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            var portRaw = section["Port"];
            var port = int.TryParse(portRaw, out var parsedPort) ? parsedPort : 5432;
            var database = section["Database"] ?? "cashflow";
            var username = section["Username"] ?? "postgres";
            var pwd = section["Pwd"] ?? section["Password"] ?? "postgres";

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = pwd
            };

            return builder.ConnectionString;
        }
    }
}
