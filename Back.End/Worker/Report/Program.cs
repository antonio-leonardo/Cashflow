using Cashflow.Back.End.Service.Transaction.DependencyInjection;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Cashflow.Report.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDependencyInjection(builder.Configuration);
builder.Services.AddSingleton<IMessageBus, StubMessageBus>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();