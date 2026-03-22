using Cashflow.Service.Transaction.Application.Commands;
using Cashflow.Service.Transaction.Application.Queries;
using Cashflow.Service.Transaction.Domain;
using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Cashflow.Service.Transaction.Postgres.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Cashflow.Service.Transaction.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();
            builder.Services.AddPostgresProviderDependencyInjection(builder.Configuration);
            builder.Services.AddDatabaseInfrastructureDependencyInjection(builder.Configuration);
            builder.Services.AddAuthorization();


            if (!builder.Environment.IsEnvironment("Testing") && !builder.Environment.IsDevelopment())
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.Authority = builder.Configuration["Keycloak:Authority"];
                        options.Audience = builder.Configuration["Keycloak:Audience"];
                        options.RequireHttpsMetadata = false;
                    });
            }

            var app = builder.Build();

            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                if (app.Environment.IsDevelopment())
                    app.MapOpenApi();

                using var scope = app.Services.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("TransactionDbContext initialized");

                var idempotencyDb = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
                await idempotencyDb.Database.EnsureCreatedAsync();
                logger.LogInformation("IdempotencyDbContext initialized");
            }

            if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("TransactionDbContext initialized");

                var idempotencyDb = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
                await idempotencyDb.Database.EnsureCreatedAsync();
                logger.LogInformation("IdempotencyDbContext initialized");
            }

            app.UseHttpsRedirection();

            if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
            {
                app.UseAuthentication();
                app.UseAuthorization();
            }


            var endpointPostTransactions = app.MapPost("/api/transactions", async (
                CreateTransactionRequest request,
                ICreateTransactionHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var correlationId = http.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? Guid.NewGuid().ToString();
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
            }).WithName("CreateTransaction");

            var endpointGetTransactions = app.MapGet("/api/transactions/{id:guid}", async (
                HttpContext ctx,
                Guid id,
                IGetTransactionQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var user = ctx.Request.Headers["X-User-Id"];
                var model = await handler.HandleAsync(new GetTransactionQuery(id), cancellationToken);
                return model is null ? Results.NotFound() : Results.Ok(model);
            }).WithName("GetTransaction");

            if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
            {
                endpointPostTransactions.RequireAuthorization();
                endpointGetTransactions.RequireAuthorization();
            }

            app.Run();
        }
    }
    record CreateTransactionRequest(Guid AccountId, decimal Amount, string Currency, TransactionType Type);
}