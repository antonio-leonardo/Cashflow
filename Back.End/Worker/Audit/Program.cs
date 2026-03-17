using Cashflow.Shared.Messaging.RabbitMQ.DependecyInjection;
using Cashflow.Worker.Audit;
using MongoDB.Driver;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMessagingDependencyInjection(builder.Configuration);
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var connection = config["Mongo:Connection"];

    return new MongoClient(connection);
});

builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("cashflow-audit");
});
builder.Services.AddScoped<TransactionCreatedHandler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
