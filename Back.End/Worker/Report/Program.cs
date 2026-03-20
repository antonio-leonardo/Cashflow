using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;
using Cashflow.Shared.NoSql.MongoDB;
using Cashflow.Worker.Report;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);

builder.Services.AddMongoDBProviderDependencyInjection(builder.Configuration, "cashflow-report");
builder.Services.AddScoped<TransactionCreatedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();