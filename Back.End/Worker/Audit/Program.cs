using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;

namespace Cashflow.Worker.Audit
{
    public class Program
    {
        protected Program() { }

        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);

            builder.Services.AddMongoDBProviderDependencyInjection(builder.Configuration, "cashflow-audit");
            builder.Services.AddScoped<TransactionCreatedHandler>();

            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
