using Cashflow.Worker.Report;
using Infrastructure.Test;
using MongoDB.Driver;
using System.Net.Http.Json;

namespace E2E.Report.Test
{
    [Collection("CompleteInfrastructureCollection")]
    public class ReportPipelineE2ETests
    {
        private readonly ReportCompleteInfrastructureFixture _infra;
        private readonly CustomWebApplicationFactory _factory;
        public ReportPipelineE2ETests(ReportCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new CustomWebApplicationFactory(_infra);
        }

        [Fact]
        public async Task Transaction_Should_Update_Report()
        {
            var client = _factory.CreateClient();

            var accountId = Guid.NewGuid();

            var request = new
            {
                AccountId = accountId,
                Amount = 200,
                Currency = "BRL",
                Type = 1
            };

            var response = await client.PostAsJsonAsync("/api/transactions", request);
            response.EnsureSuccessStatusCode();

            var mongoClient = new MongoClient(_infra.MongoDbContainerFixture.ConnectionString);
            var database = mongoClient.GetDatabase("cashflow-report");

            var collection = database.GetCollection<TransactionReportDocument>("transactions");

            TransactionReportDocument result = null;

            var retries = 10;

            for (int i = 0; i < retries; i++)
            {
                var filter = Builders<TransactionReportDocument>
                    .Filter.Eq("AccountId", accountId);

                result = await collection.Find(filter).FirstOrDefaultAsync();

                if (result != null)
                    break;

                await Task.Delay(500);
            }

            Assert.NotNull(result);
            Assert.Equal(accountId, result.AccountId);
            Assert.Equal(200, result.Amount);
        }
    }
}