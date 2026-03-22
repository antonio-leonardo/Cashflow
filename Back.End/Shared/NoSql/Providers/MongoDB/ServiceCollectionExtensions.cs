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

        public static IServiceCollection AddMongoDBProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration, string databaseName)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connection = config["Mongo:Connection"]!;

                return _clients.GetOrAdd(connection, conn => CreateConnection(conn));
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