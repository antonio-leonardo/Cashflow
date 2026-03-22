using Cashflow.Service.Transaction.Infrastructure.Persistence;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Infrastructure.Test
{
    public class PostgresContainerFixture : IAsyncLifetime
    {
        private readonly string _alias;
        public PostgreSqlContainer Postgres { get; }

        public PostgresContainerFixture(INetwork network, string alias)
        {
            _alias = alias;
            Postgres = new PostgreSqlBuilder("postgres:16")
                .WithDatabase("cashflow")
                .WithUsername("admin")
                .WithPassword("admin")
                .WithNetwork(network)
                .WithNetworkAliases(alias)
                .Build();
        }

        public async Task InitializeAsync()
        {
            await Postgres.StartAsync();

            var transactionOptions = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            await using var transactionDb = new TransactionDbContext(transactionOptions);
            await transactionDb.Database.EnsureCreatedAsync();

            var idempotencyOptions = new DbContextOptionsBuilder<IdempotencyDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            await using var idempotencyDb = new IdempotencyDbContext(idempotencyOptions);
            await idempotencyDb.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync() => await Postgres.DisposeAsync();

        public string ConnectionString => Postgres.GetConnectionString();

        public string NetworkConnectionString
        {
            get
            {
                var builder = new NpgsqlConnectionStringBuilder(Postgres.GetConnectionString())
                {
                    Host = _alias,
                    Port = 5432
                };
                return builder.ConnectionString;
            }
        }
    }
}