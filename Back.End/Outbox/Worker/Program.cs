using Cashflow.Outbox.Worker;
using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.RabbitMQ.DependecyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSqlDatabaseDependencyInjection(builder.Configuration);
builder.Services.AddMessagingDependencyInjection(builder.Configuration);
builder.Services.AddSingleton<IMessageBus, ConsoleMessageBus>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();