using Cashflow.Shared.Messaging;
using Cashflow.Transaction.Infrastructure.DependencyInjection;
using Outbox.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransactionInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IMessageBus, ConsoleMessageBus>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();