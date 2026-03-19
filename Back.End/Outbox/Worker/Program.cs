using Cashflow.Outbox.Worker;
using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.RabbitMQ.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPostgresProviderDependencyInjection(builder.Configuration);
builder.Services.AddDatabaseInfrastructureDependencyInjection(builder.Configuration);
builder.Services.AddRabbitMQDependencyInjection(builder.Configuration);
builder.Services.AddSingleton<IMessageBus, ConsoleMessageBus>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();