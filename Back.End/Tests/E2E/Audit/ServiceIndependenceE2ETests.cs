using Cashflow.Shared.Resilience;
using Infrastructure.Test;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;

namespace E2E.Audit.Test
{
    [Collection("ServiceIndependenceInfrastructureCollection")]
    public class ServiceIndependenceE2ETests : IDisposable
    {
        private readonly AuditCompleteInfrastructureFixture _infra;
        private readonly TransactionWebApplicationFactory _factory;

        public ServiceIndependenceE2ETests(AuditCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new TransactionWebApplicationFactory(_infra, enableAuditWorker: false);
        }

        [Fact]
        public async Task Transaction_Service_Should_Stay_Available_When_Audit_Worker_Is_Down()
        {
            await _infra.AuditWorkerFixture.StopAsync();
            await Task.Delay(1000);

            var accountId = Guid.NewGuid();
            var request = new
            {
                AccountId = accountId,
                Amount = 145m,
                Currency = "BRL",
                Type = 1
            };

            try
            {
                var client = _factory.CreateClient();
                var response = await ResiliencePolicies
                    .GetHttpResiliencePolicy()
                    .ExecuteAsync(() => client.PostAsJsonAsync("/api/transactions", request));

                response.EnsureSuccessStatusCode();

                var mongoClient = CreateConnection(_infra.MongoDbContainerFixture.ConnectionString);
                var database = mongoClient.GetDatabase("cashflow-audit");
                var collection = database.GetCollection<BsonDocument>("events");

                await Task.Delay(3000);

                var eventBeforeRecovery = await FindAuditEventByAccountAsync(collection, accountId);
                Xunit.Assert.Null(eventBeforeRecovery);
            }
            finally
            {
                await _infra.AuditWorkerFixture.StartAsync();
            }
        }

        [Fact]
        public async Task Audit_Should_Catch_Up_After_Worker_Recovers()
        {
            await _infra.AuditWorkerFixture.StopAsync();
            await Task.Delay(1000);

            var accountId = Guid.NewGuid();
            var request = new
            {
                AccountId = accountId,
                Amount = 160m,
                Currency = "BRL",
                Type = 1
            };

            try
            {
                var client = _factory.CreateClient();
                var response = await ResiliencePolicies
                    .GetHttpResiliencePolicy()
                    .ExecuteAsync(() => client.PostAsJsonAsync("/api/transactions", request));

                response.EnsureSuccessStatusCode();

                var mongoClient = CreateConnection(_infra.MongoDbContainerFixture.ConnectionString);
                var database = mongoClient.GetDatabase("cashflow-audit");
                var collection = database.GetCollection<BsonDocument>("events");

                var beforeRecovery = await FindAuditEventByAccountAsync(collection, accountId);
                Xunit.Assert.Null(beforeRecovery);

                await _infra.AuditWorkerFixture.StartAsync();
                await Task.Delay(1000);

                var afterRecovery = await WaitForAuditEventAsync(collection, accountId);
                Xunit.Assert.NotNull(afterRecovery);

                var payload = afterRecovery!["Payload"].AsBsonDocument;
                Xunit.Assert.Equal(accountId, payload["AccountId"].AsGuid);
            }
            finally
            {
                await _infra.AuditWorkerFixture.StartAsync();
            }
        }

        private static async Task<BsonDocument?> WaitForAuditEventAsync(
            IMongoCollection<BsonDocument> collection,
            Guid accountId,
            int retries = 20)
        {
            for (int i = 0; i < retries; i++)
            {
                var doc = await FindAuditEventByAccountAsync(collection, accountId);
                if (doc is not null)
                {
                    return doc;
                }

                await Task.Delay(2000);
            }

            return null;
        }

        private static async Task<BsonDocument?> FindAuditEventByAccountAsync(
            IMongoCollection<BsonDocument> collection,
            Guid accountId)
        {
            var documents = await collection.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();

            return documents.FirstOrDefault(doc =>
                doc.Contains("Payload") &&
                doc["Payload"].AsBsonDocument.Contains("AccountId") &&
                doc["Payload"]["AccountId"].AsGuid == accountId);
        }

        private static MongoClient CreateConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (MongoClient)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(new MongoClient(connection));
            }).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _factory.Dispose();
        }
    }
}
