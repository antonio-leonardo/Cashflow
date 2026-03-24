using Cashflow.Shared.Resilience;
using Infrastructure.Test;
using StackExchange.Redis;
using System.Net.Http.Json;

namespace E2E.Balance.Tests
{
    [Collection("CompleteInfrastructureCollection")]
    public class BalancePipelineE2ETests : IDisposable
    {
        private readonly BalanceCompleteInfrastructureFixture _infra;
        private readonly TransactionWebApplicationFactory _factory;

        public BalancePipelineE2ETests(BalanceCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new TransactionWebApplicationFactory(_infra);
        }

        [Fact]
        public async Task Transaction_Should_Update_ReadModels()
        {
            await _infra.WorkerBalanceFixture.StartAsync();
            await Task.Delay(1000);

            var client = _factory.CreateClient();
            var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var objectToRequest = new
            {
                AccountId = Guid.NewGuid(),
                Amount = 100,
                Currency = "BRL",
                Type = 1
            };

            var response = await ResiliencePolicies
                .GetHttpResiliencePolicy()
                .ExecuteAsync(() => client.PostAsJsonAsync("/api/transactions", objectToRequest));

            response.EnsureSuccessStatusCode();

            await Task.Delay(10000);

            var redis = CreateConnection(_infra.RedisContainerFixture.ConnectionString);
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync($"balance:{objectToRequest.AccountId}");

            if (value.IsNull)
            {
                var retries = 10;

                for (int i = 0; i < retries; i++)
                {
                    value = await db.StringGetAsync($"balance:{objectToRequest.AccountId}");

                    if (!value.IsNull)
                    {
                        break;
                    }

                    await Task.Delay(5000);
                }
            }

            Xunit.Assert.False(value.IsNull);

            var dailyBalanceKey = $"balance:daily:{objectToRequest.AccountId}:{referenceDate:yyyy-MM-dd}";
            var dailyValue = await db.StringGetAsync(dailyBalanceKey);

            if (dailyValue.IsNull)
            {
                var retries = 10;

                for (int i = 0; i < retries; i++)
                {
                    dailyValue = await db.StringGetAsync(dailyBalanceKey);

                    if (!dailyValue.IsNull)
                    {
                        break;
                    }

                    await Task.Delay(5000);
                }
            }

            Xunit.Assert.False(dailyValue.IsNull);
        }

        private ConnectionMultiplexer CreateConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (ConnectionMultiplexer)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(ConnectionMultiplexer.Connect(connection));
            }).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _factory.Dispose();
        }
    }
}
