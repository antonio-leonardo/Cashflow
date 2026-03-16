using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ.MessageBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ.DependecyInjection
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