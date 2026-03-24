using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.Redis;

namespace Cashflow.Worker.Balance
{
    public class Program
    {
        protected Program() { }

        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);
            builder.Services.AddRedisProviderDependencyInjection(builder.Configuration);
            builder.Services.AddScoped<RedisBalanceRepository>();
            builder.Services.AddScoped<TransactionCreatedHandler>();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
