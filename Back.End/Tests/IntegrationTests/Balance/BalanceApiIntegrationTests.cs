using Cashflow.Shared.Contracts.Api;
using Infrastructure.Test;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Cashflow.Worker.Report;

namespace Balance.Integration.Tests
{
    [Collection("BalanceApiIntegrationCollection")]
    public sealed class BalanceApiIntegrationTests : IAsyncLifetime
    {
        private readonly BalanceApiIntegrationInfrastructureFixture _fixture;
        private HttpClient _client = default!;

        public BalanceApiIntegrationTests(BalanceApiIntegrationInfrastructureFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync()
        {
            _client = new HttpClient
            {
                BaseAddress = _fixture.BalanceQueryApiFixture.BaseAddress,
                Timeout = TimeSpan.FromSeconds(10)
            };

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _client.Dispose();
            return Task.CompletedTask;
        }

    [Fact]
        public async Task Should_Return_NotFound_For_Unmaterialized_Daily_Balance()
        {
            var accountId = Guid.NewGuid();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);

            var response = await _client.GetAsync($"/api/v1/balance/daily/{accountId}?date={date:yyyy-MM-dd}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

    [Fact]
        public async Task Should_Return_Daily_Balance_From_V1_Route_When_Data_Exists()
        {
            var accountId = Guid.NewGuid();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);

            await SeedDailyBalanceAsync(accountId, date, credits: 500m, debits: 120m, net: 380m);

            var response = await _client.GetAsync($"/api/v1/balance/daily/{accountId}?date={date:yyyy-MM-dd}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<GetDailyBalanceResponse>();
            var payload = Assert.IsType<GetDailyBalanceResponse>(body);

            Assert.Equal(accountId, payload.AccountId);
            Assert.Equal(date, payload.Date);
            Assert.Equal(500m, payload.TotalCredits);
            Assert.Equal(120m, payload.TotalDebits);
            Assert.Equal(380m, payload.NetBalance);
        }

    [Fact]
        public async Task Should_Keep_Legacy_And_V1_Balance_Routes_Compatible()
        {
            var accountId = Guid.NewGuid();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);

            await SeedDailyBalanceAsync(accountId, date, credits: 90m, debits: 30m, net: 60m);

            var legacyResponse = await _client.GetAsync($"/api/balance/daily/{accountId}?date={date:yyyy-MM-dd}");
            var versionedResponse = await _client.GetAsync($"/api/v1/balance/daily/{accountId}?date={date:yyyy-MM-dd}");

            Assert.Equal(HttpStatusCode.OK, legacyResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, versionedResponse.StatusCode);

            var legacyPayload = Assert.IsType<GetDailyBalanceResponse>(
                await legacyResponse.Content.ReadFromJsonAsync<GetDailyBalanceResponse>());
            var versionedPayload = Assert.IsType<GetDailyBalanceResponse>(
                await versionedResponse.Content.ReadFromJsonAsync<GetDailyBalanceResponse>());

            Assert.Equal(legacyPayload.AccountId, versionedPayload.AccountId);
            Assert.Equal(legacyPayload.Date, versionedPayload.Date);
            Assert.Equal(legacyPayload.TotalCredits, versionedPayload.TotalCredits);
            Assert.Equal(legacyPayload.TotalDebits, versionedPayload.TotalDebits);
            Assert.Equal(legacyPayload.NetBalance, versionedPayload.NetBalance);
        }

        [Fact]
        public async Task Should_Export_Daily_Report_From_V1_Route_When_ReportProjectionExists()
        {
            var accountId = Guid.NewGuid();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);

            await SeedReportProjectionAsync(accountId, date, 150m);
            await SeedReportProjectionAsync(accountId, date, 75m);

            var response = await _client.GetAsync($"/api/v1/balance/reports/daily/{accountId}?date={date:yyyy-MM-dd}&downloadLinkExpiryMinutes=30");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = Assert.IsType<GetDailyReportExportResponse>(
                await response.Content.ReadFromJsonAsync<GetDailyReportExportResponse>());

            Assert.Equal(accountId, payload.AccountId);
            Assert.Equal(date, payload.Date);
            Assert.Equal(2, payload.TransactionCount);
            Assert.Equal($"{accountId}/{date:yyyy/MM/dd}/report.csv", payload.Path);
            Assert.True(payload.DownloadUri.IsAbsoluteUri);
        }

        private async Task SeedDailyBalanceAsync(
            Guid accountId,
            DateOnly date,
            decimal credits,
            decimal debits,
            decimal net)
        {
            var key = $"balance:daily:{accountId}:{date:yyyy-MM-dd}";

            using var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisContainerFixture.ConnectionString);
            var db = redis.GetDatabase();

            await db.HashSetAsync(
                key,
                new HashEntry[]
                {
                    new("credits", credits.ToString(CultureInfo.InvariantCulture)),
                    new("debits", debits.ToString(CultureInfo.InvariantCulture)),
                    new("net", net.ToString(CultureInfo.InvariantCulture))
                });
        }

        private Task SeedReportProjectionAsync(Guid accountId, DateOnly date, decimal amount)
        {
            var client = new MongoClient(_fixture.MongoDbContainerFixture.ConnectionString);
            var database = client.GetDatabase("cashflow-report");
            var collection = database.GetCollection<TransactionReportDocument>("transactions");

            return collection.InsertOneAsync(new TransactionReportDocument
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Amount = amount,
                Currency = "BRL",
                CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(1)
            });
        }
    }
}
