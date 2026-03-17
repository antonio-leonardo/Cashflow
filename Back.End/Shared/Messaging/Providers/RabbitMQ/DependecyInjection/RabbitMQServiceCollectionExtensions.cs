using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Messaging.RabbitMQ.DependecyInjection
{
    public static class RabbitMQServiceCollectionExtensions
    {
        public static IServiceCollection AddMessagingDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
            services.AddSingleton<IMessageBus, RabbitMqBus>();
            return services;
        }
    }
}