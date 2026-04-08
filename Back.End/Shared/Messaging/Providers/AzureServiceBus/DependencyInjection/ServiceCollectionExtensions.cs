using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.AzureServiceBus.MessageBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Messaging.AzureServiceBus.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureServiceBusDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection("AzureServiceBus");

            services.Configure<AzureServiceBusOptions>(options =>
            {
                section.Bind(options);
            });

            services.AddSingleton<IMessageBus, AzureServiceBusBus>();
            return services;
        }
    }
}
