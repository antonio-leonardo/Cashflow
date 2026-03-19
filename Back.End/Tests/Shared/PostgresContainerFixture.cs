using Cashflow.Service.Transaction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Test
{
    public class PostgresContainerFixture : IAsyncLifetime
    {
        public PostgreSqlContainer Postgres { get; }

        public PostgresContainerFixture()
        {
            Postgres = new PostgreSqlBuilder("postgres:16")
                .WithDatabase("cashflow")
                .WithUsername("admin")
                .WithPassword("admin")
                .Build();
        }

        public async Task InitializeAsync()
        {
            await Postgres.StartAsync();

            var transactionOptions = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseNpgsql(Postgres.GetConnectionString())
                .Options;

            await using var transactionDb = new TransactionDbContext(transactionOptions);
            await transactionDb.Database.EnsureCreatedAsync();

            var idempotencyOptions = new DbContextOptionsBuilder<IdempotencyDbContext>()
                .UseNpgsql(Postgres.GetConnectionString())
                .Options;

            await using var idempotencyDb = new IdempotencyDbContext(idempotencyOptions);
            await idempotencyDb.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            await Postgres.DisposeAsync();
        }

        public string ConnectionString => Postgres.GetConnectionString();
    }
}