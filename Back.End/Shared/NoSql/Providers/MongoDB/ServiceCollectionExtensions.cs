using Cashflow.Shared.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace Cashflow.Shared.NoSql.MongoDB
{
    public static class ServiceCollectionExtensions
    {
        private static readonly ConcurrentDictionary<string, IMongoClient> _clients = new();
        private static readonly string[] SupportedDocumentProviders =
        [
            "MongoDB",
            "CosmosDb"
        ];

        public static IServiceCollection AddMongoDBProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration,
            string databaseName)
        {
            var documentProvider = configuration["Providers:Document"] ?? "MongoDB";

            if (!SupportedDocumentProviders.Any(supported =>
                    string.Equals(supported, documentProvider, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Unsupported document provider '{documentProvider}'. Supported values: {string.Join(", ", SupportedDocumentProviders)}.");
            }

            if (string.Equals(documentProvider, "CosmosDb", StringComparison.OrdinalIgnoreCase))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    configuration["CosmosDb:MongoDB:Connection"],
                    "CosmosDb:MongoDB:Connection");
            }

            services.AddSingleton<IMongoClient>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = string.Equals(documentProvider, "CosmosDb", StringComparison.OrdinalIgnoreCase)
                    ? config["CosmosDb:MongoDB:Connection"]
                    : config["Mongo:Connection"];

                ArgumentException.ThrowIfNullOrWhiteSpace(connectionString,
                    string.Equals(documentProvider, "CosmosDb", StringComparison.OrdinalIgnoreCase)
                        ? "CosmosDb:MongoDB:Connection"
                        : "Mongo:Connection");

                return _clients.GetOrAdd(connectionString, conn => CreateConnection(conn));
            });

            services.AddScoped(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase(databaseName);
            });

            return services;
        }

        private static MongoClient CreateConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (MongoClient)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(new MongoClient(connection));
            }).GetAwaiter().GetResult();
        }
    }
}
