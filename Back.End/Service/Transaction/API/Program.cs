using Cashflow.Back.End.Service.Transaction.Application.Commands;
using Cashflow.Back.End.Service.Transaction.Application.Queries;
using Cashflow.Back.End.Service.Transaction.Domain;
using Cashflow.Back.End.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Back.End.Service.Transaction.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDependencyInjection(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("TransactionDbContext initialized");

        var idempotencyDb = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
        await idempotencyDb.Database.EnsureCreatedAsync();
        logger.LogInformation("IdempotencyDbContext initialized");
    }
}

app.UseHttpsRedirection();

app.MapPost("/api/transactions", async (
    CreateTransactionRequest request,
    ICreateTransactionHandler handler,
    HttpContext http,
    CancellationToken cancellationToken) =>
{
    var correlationId = http.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    var userId = http.User.FindFirst("sub")?.Value;

    var command = new CreateTransactionCommand(
        TransactionId: Guid.NewGuid(),
        request.AccountId,
        request.Amount,
        request.Currency,
        request.Type,
        correlationId,
        userId);

    await handler.HandleAsync(command, cancellationToken);

    return Results.Created($"/api/transactions/{command.TransactionId}", new { command.TransactionId });
})
.WithName("CreateTransaction");

app.MapGet("/api/transactions/{id:guid}", async (Guid id, IGetTransactionQueryHandler handler, CancellationToken cancellationToken) =>
{
    var model = await handler.HandleAsync(new GetTransactionQuery(id), cancellationToken);
    return model is null ? Results.NotFound() : Results.Ok(model);
})
.WithName("GetTransaction");

app.Run();

record CreateTransactionRequest(Guid AccountId, decimal Amount, string Currency, TransactionType Type);