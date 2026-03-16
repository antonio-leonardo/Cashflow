using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Net.Http.Json;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace E2E.Tests
{
    public class CashflowPipelineE2ETests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres =
            new PostgreSqlBuilder()
            .WithDatabase("cashflow")
            .WithUsername("admin")
            .WithPassword("admin")
            .Build();

        private readonly RabbitMqContainer _rabbit =
            new RabbitMqBuilder().Build();

        private readonly MongoDbContainer _mongo =
            new MongoDbBuilder().Build();

        private WebApplicationFactory<Program> _factory;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
            await _rabbit.StartAsync();
            await _mongo.StartAsync();

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting(
                        "ConnectionStrings:TransactionDb",
                        _postgres.GetConnectionString());

                    builder.UseSetting(
                        "RabbitMq:Host",
                        _rabbit.Hostname);

                    builder.UseSetting(
                        "Mongo:ConnectionString",
                        _mongo.GetConnectionString());
                });
        }

        public async Task DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _rabbit.DisposeAsync();
            await _mongo.DisposeAsync();
        }

        [Fact]
        public async Task Transaction_Should_Update_ReadModels()
        {
            var client = _factory.CreateClient();

            var accountId = Guid.NewGuid();

            var request = new
            {
                accountId = accountId,
                amount = 500,
                currency = "USD"
            };

            var response = await client.PostAsJsonAsync(
                "/transactions",
                request);

            response.EnsureSuccessStatusCode();

            await Task.Delay(5000);

            var redis = await ConnectionMultiplexer
                .ConnectAsync("localhost:6379");

            var db = redis.GetDatabase();

            var balance = await db.StringGetAsync($"balance:{accountId}");

            Assert.False(balance.IsNull);

            var mongoClient = new MongoClient(_mongo.GetConnectionString());

            var database = mongoClient.GetDatabase("cashflow");

            var reports = database.GetCollection<dynamic>("reports");

            var report = await reports
                .Find(Builders<dynamic>.Filter.Eq("accountId", accountId))
                .FirstOrDefaultAsync();

            Assert.NotNull(report);
        }
    }
}