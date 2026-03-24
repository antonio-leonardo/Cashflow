using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.RabbitMQ.MessageBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRabbitMQDependencyInjection(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var rabbitMqSection = configuration.GetSection("RabbitMq");
            if (!rabbitMqSection.Exists())
            {
                rabbitMqSection = configuration.GetSection("Infrastructure:RabbitMq");
            }

            services.Configure<RabbitMqOptions>(options =>
            {
                rabbitMqSection.Bind(options);

                var pwd = rabbitMqSection["Pwd"];
                if (!string.IsNullOrWhiteSpace(pwd))
                {
                    options.Password = pwd;
                }
            });

            services.AddSingleton<IMessageBus, RabbitMqBus>();
            return services;
        }
    }
}
