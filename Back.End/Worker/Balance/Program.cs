using Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ.DependecyInjection;
using Cashflow.Back.End.Worker.Balance;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMessagingDependencyInjection(builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connection = config["Redis:Connection"];

    return ConnectionMultiplexer.Connect(connection);
});
builder.Services.AddScoped<TransactionCreatedHandler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();