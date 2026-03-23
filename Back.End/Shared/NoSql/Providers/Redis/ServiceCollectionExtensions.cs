using Cashflow.Shared.Resilience;
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
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connection = config["Redis:Connection"];
                ArgumentException.ThrowIfNullOrWhiteSpace(connection);
                return CreateConnection(connection);
            });
            return services;
        }

        private static ConnectionMultiplexer CreateConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (ConnectionMultiplexer)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(ConnectionMultiplexer.Connect(connection));
            }).GetAwaiter().GetResult();
        }
    }
}
