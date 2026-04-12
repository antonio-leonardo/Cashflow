using Cashflow.Shared.Contracts.Configuration;
using Cashflow.Shared.NoSql.Abstractions;
using Cashflow.Shared.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace Cashflow.Shared.NoSql.MongoDB
{
    public static class ServiceCollectionExtensions
    {
        private static readonly ConcurrentDictionary<string, IMongoClient> Clients = new();

        public static IServiceCollection AddMongoDBProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration,
            string databaseName)
        {
            var documentProvider = configuration.GetConfiguredProvider(
                "Providers:Document",
                DocumentProvider.MongoDB,
                "document provider");

            switch (documentProvider)
            {
                case DocumentProvider.CosmosDb:
                    ArgumentException.ThrowIfNullOrWhiteSpace(
                        configuration["CosmosDb:MongoDB:Connection"],
                        "CosmosDb:MongoDB:Connection");
                    break;

                case DocumentProvider.MongoDB:
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported document provider '{documentProvider}' configured at 'Providers:Document'.");
            }

            services.AddSingleton<IMongoClient>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = documentProvider switch
                {
                    DocumentProvider.CosmosDb => config["CosmosDb:MongoDB:Connection"],
                    DocumentProvider.MongoDB => config["Mongo:Connection"],
                    _ => throw new InvalidOperationException($"Unsupported document provider '{documentProvider}' configured at 'Providers:Document'.")
                };

                ArgumentException.ThrowIfNullOrWhiteSpace(
                    connectionString,
                    documentProvider switch
                    {
                        DocumentProvider.CosmosDb => "CosmosDb:MongoDB:Connection",
                        DocumentProvider.MongoDB => "Mongo:Connection",
                        _ => "Providers:Document"
                    });

                return Clients.GetOrAdd(connectionString, CreateConnection);
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
