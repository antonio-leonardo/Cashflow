using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Worker.Audit;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);

builder.Services.AddMongoDBProviderDependencyInjection(builder.Configuration, "cashflow-audit");
builder.Services.AddScoped<TransactionCreatedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
