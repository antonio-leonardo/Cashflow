using Azure.Identity;
using Cashflow.Shared.Contracts.Configuration;
using Cashflow.Shared.NoSql.Abstractions;
using Cashflow.Shared.Resilience;
using Microsoft.Azure.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Cashflow.Shared.NoSql.Redis
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRedisProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var cacheProvider = configuration.GetConfiguredProvider(
                "Providers:Cache",
                CacheProvider.Redis,
                "cache provider");

            switch (cacheProvider)
            {
                case CacheProvider.AzureRedis:
                    ArgumentException.ThrowIfNullOrWhiteSpace(
                        configuration["AzureRedis:Endpoint"],
                        "AzureRedis:Endpoint");
                    break;

                case CacheProvider.Redis:
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported cache provider '{cacheProvider}' configured at 'Providers:Cache'.");
            }

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();

                return cacheProvider switch
                {
                    CacheProvider.AzureRedis => CreateAzureConnection(config),
                    CacheProvider.Redis => CreateLocalConnection(config),
                    _ => throw new InvalidOperationException($"Unsupported cache provider '{cacheProvider}' configured at 'Providers:Cache'.")
                };
            });

            return services;
        }

        private static IConnectionMultiplexer CreateLocalConnection(IConfiguration configuration)
        {
            var connection = configuration["Redis:Connection"];
            ArgumentException.ThrowIfNullOrWhiteSpace(connection, "Redis:Connection");

            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (IConnectionMultiplexer)policy.ExecuteAsync(() =>
                Task.FromResult<object>(ConnectionMultiplexer.Connect(connection))
            ).GetAwaiter().GetResult();
        }

        private static IConnectionMultiplexer CreateAzureConnection(IConfiguration configuration)
        {
            var endpoint = configuration["AzureRedis:Endpoint"];
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, "AzureRedis:Endpoint");

            var useManagedIdentity = configuration.GetValue<bool>("AzureRedis:UseManagedIdentity");
            var configOptions = ConfigurationOptions.Parse(endpoint);

            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (IConnectionMultiplexer)policy.ExecuteAsync(async () =>
            {
                if (useManagedIdentity)
                {
                    await configOptions.ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential());
                }

                return (object)await ConnectionMultiplexer.ConnectAsync(configOptions);
            }).GetAwaiter().GetResult();
        }
    }
}
