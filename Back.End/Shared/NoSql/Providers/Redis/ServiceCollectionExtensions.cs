using Azure.Identity;
using Cashflow.Shared.Resilience;
using Microsoft.Azure.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Cashflow.Shared.NoSql.Redis
{
    public static class ServiceCollectionExtensions
    {
        private static readonly string[] SupportedCacheProviders =
        [
            "Redis",
            "AzureRedis"
        ];

        public static IServiceCollection AddRedisProviderDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var cacheProvider = configuration["Providers:Cache"] ?? "Redis";

            if (!SupportedCacheProviders.Any(supported =>
                    string.Equals(supported, cacheProvider, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Unsupported cache provider '{cacheProvider}'. Supported values: {string.Join(", ", SupportedCacheProviders)}.");
            }

            if (string.Equals(cacheProvider, "AzureRedis", StringComparison.OrdinalIgnoreCase))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    configuration["AzureRedis:Endpoint"],
                    "AzureRedis:Endpoint");
            }

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return string.Equals(cacheProvider, "AzureRedis", StringComparison.OrdinalIgnoreCase)
                    ? CreateAzureConnection(config)
                    : CreateLocalConnection(config);
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
