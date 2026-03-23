using Cashflow.Shared.Resilience;
using Cashflow.Worker.Report;
using Infrastructure.Test;
using MongoDB.Driver;
using System.Net.Http.Json;

namespace E2E.Report.Test
{
    [Collection("CompleteInfrastructureCollection")]
    public class ReportPipelineE2ETests : IDisposable
    {
        private readonly ReportCompleteInfrastructureFixture _infra;
        private readonly TransactionWebApplicationFactory _factory;
        public ReportPipelineE2ETests(ReportCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new TransactionWebApplicationFactory(_infra);
        }

        [Fact]
        public async Task Transaction_Should_Update_Report()
        {
            await _infra.ReportWorkerFixture.StartAsync();
            await Task.Delay(1000);

            var client = _factory.CreateClient();

            var accountId = Guid.NewGuid();

            var request = new
            {
                AccountId = accountId,
                Amount = 200,
                Currency = "BRL",
                Type = 1
            };

            var response = await ResiliencePolicies
                .GetHttpResiliencePolicy()
                .ExecuteAsync(() => client.PostAsJsonAsync("/api/transactions", request));
            response.EnsureSuccessStatusCode();

            await Task.Delay(10000);

            var mongoClient = CreateConnection(_infra.MongoDbContainerFixture.ConnectionString);
            var database = mongoClient.GetDatabase("cashflow-report");

            var collection = database.GetCollection<TransactionReportDocument>("transactions");

            TransactionReportDocument? result = null;

            var retries = 10;

            for (int i = 0; i < retries; i++)
            {
                var filter = Builders<TransactionReportDocument>
                    .Filter.Eq("AccountId", accountId);

                result = await collection.Find(filter).FirstOrDefaultAsync();

                if (result != null)
                    break;

                await Task.Delay(5000);
            }

            var validatedResult = Xunit.Assert.IsType<TransactionReportDocument>(result);
            Xunit.Assert.Equal(accountId, validatedResult.AccountId);
            Xunit.Assert.Equal(200, validatedResult.Amount);
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
