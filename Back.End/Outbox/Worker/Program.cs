using Cashflow.Back.End.Outbox.Worker;
using Cashflow.Back.End.Service.Transaction.Providers.Postgres.DependencyInjection;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ.DependecyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSqlDatabaseDependencyInjection(builder.Configuration);
builder.Services.AddMessagingDependencyInjection(builder.Configuration);
builder.Services.AddSingleton<IMessageBus, ConsoleMessageBus>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();