using Cashflow.Balance.Worker;
using Cashflow.Shared.Messaging;
using Cashflow.Transaction.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransactionInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IMessageBus, StubMessageBus>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();