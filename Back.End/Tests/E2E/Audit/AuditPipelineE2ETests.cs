using Cashflow.Shared.Resilience;
using Infrastructure.Test;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;

namespace E2E.Audit.Test
{
    [Collection("CompleteInfrastructureCollection")]
    public class AuditPipelineE2ETests : IDisposable
    {
        private readonly AuditCompleteInfrastructureFixture _infra;
        private readonly TransactionWebApplicationFactory _factory;
        public AuditPipelineE2ETests(AuditCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new TransactionWebApplicationFactory(_infra, enableAuditWorker: true);
        }

    [Fact]
        public async Task Transaction_Should_Create_Audit_Log()
        {
            await _infra.AuditWorkerFixture.StartAsync();
            await Task.Delay(1000);

            var client = _factory.CreateClient();

            var accountId = Guid.NewGuid();

            var request = new
            {
                AccountId = accountId,
                Amount = 150,
                Currency = "BRL",
                Type = 1
            };

            var response = await ResiliencePolicies
                .GetHttpResiliencePolicy()
                .ExecuteAsync(() => client.PostAsJsonAsync("/api/transactions", request));

            response.EnsureSuccessStatusCode();

            await Task.Delay(10000);

            var mongoClient = CreateConnection(_infra.MongoDbContainerFixture.ConnectionString);
            var database = mongoClient.GetDatabase("cashflow-audit");

            var collection = database.GetCollection<BsonDocument>("events");

            BsonDocument? result = null;

            var retries = 10;

            for (int i = 0; i < retries; i++)
            {
                var filter = Builders<BsonDocument>.Filter.Empty;

                var documents = await collection.Find(filter).ToListAsync();

                result = documents.FirstOrDefault(doc =>
                    doc.Contains("Payload") &&
                    doc["Payload"].AsBsonDocument.Contains("AccountId") &&
                    doc["Payload"]["AccountId"].AsGuid == accountId
                );

                if (result != null)
                    break;

                await Task.Delay(5000);
            }

            var validatedResult = Xunit.Assert.IsType<BsonDocument>(result);
            var payload = validatedResult["Payload"].AsBsonDocument;

            Xunit.Assert.Equal(accountId, payload["AccountId"].AsGuid);
        }

        private MongoClient CreateConnection(string connection)
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
