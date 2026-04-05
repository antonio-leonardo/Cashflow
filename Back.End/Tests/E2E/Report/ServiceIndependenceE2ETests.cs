using Cashflow.Shared.Resilience;
using Cashflow.Worker.Report;
using Infrastructure.Test;
using MongoDB.Driver;
using System.Net.Http.Json;

namespace E2E.Report.Test
{
    [Collection("ServiceIndependenceInfrastructureCollection")]
    public class ServiceIndependenceE2ETests : IDisposable
    {
        private readonly ReportCompleteInfrastructureFixture _infra;
        private readonly TransactionWebApplicationFactory _factory;

        public ServiceIndependenceE2ETests(ReportCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new TransactionWebApplicationFactory(_infra, enableReportWorker: false);
        }

    [Fact]
        public async Task Transaction_Service_Should_Stay_Available_When_Report_Worker_Is_Down()
        {
            await _infra.ReportWorkerFixture.StopAsync();
            await Task.Delay(1000);

            var accountId = Guid.NewGuid();
            var request = new
            {
                AccountId = accountId,
                Amount = 230m,
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
                var database = mongoClient.GetDatabase("cashflow-report");
                var collection = database.GetCollection<TransactionReportDocument>("transactions");

                await Task.Delay(3000);

                var beforeRecovery = await FindReportAsync(collection, accountId);
                Xunit.Assert.Null(beforeRecovery);
            }
            finally
            {
                await _infra.ReportWorkerFixture.StartAsync();
            }
        }

    [Fact]
        public async Task Report_Should_Catch_Up_After_Worker_Recovers()
        {
            await _infra.ReportWorkerFixture.StopAsync();
            await Task.Delay(1000);

            var accountId = Guid.NewGuid();
            var request = new
            {
                AccountId = accountId,
                Amount = 275m,
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
                var database = mongoClient.GetDatabase("cashflow-report");
                var collection = database.GetCollection<TransactionReportDocument>("transactions");

                var beforeRecovery = await FindReportAsync(collection, accountId);
                Xunit.Assert.Null(beforeRecovery);

                await _infra.ReportWorkerFixture.StartAsync();
                await Task.Delay(1000);

                var afterRecovery = await WaitForReportAsync(collection, accountId);
                Xunit.Assert.NotNull(afterRecovery);
                Xunit.Assert.Equal(accountId, afterRecovery!.AccountId);
                Xunit.Assert.Equal(275m, afterRecovery.Amount);
            }
            finally
            {
                await _infra.ReportWorkerFixture.StartAsync();
            }
        }

        private static async Task<TransactionReportDocument?> WaitForReportAsync(
            IMongoCollection<TransactionReportDocument> collection,
            Guid accountId,
            int retries = 20)
        {
            for (int i = 0; i < retries; i++)
            {
                var report = await FindReportAsync(collection, accountId);
                if (report is not null)
                {
                    return report;
                }

                await Task.Delay(2000);
            }

            return null;
        }

        private static async Task<TransactionReportDocument?> FindReportAsync(
            IMongoCollection<TransactionReportDocument> collection,
            Guid accountId)
        {
            var filter = Builders<TransactionReportDocument>.Filter.Eq(x => x.AccountId, accountId);
            return await collection.Find(filter).FirstOrDefaultAsync();
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
