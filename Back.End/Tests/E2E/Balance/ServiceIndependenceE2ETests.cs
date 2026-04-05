using Cashflow.Shared.Resilience;
using Infrastructure.Test;
using StackExchange.Redis;
using System.Net.Http.Json;

namespace E2E.Balance.Tests
{
    [Collection("ServiceIndependenceInfrastructureCollection")]
    public class ServiceIndependenceE2ETests : IDisposable
    {
        private readonly BalanceCompleteInfrastructureFixture _infra;
        private readonly TransactionWebApplicationFactory _factory;

        public ServiceIndependenceE2ETests(BalanceCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new TransactionWebApplicationFactory(_infra, enableBalanceWorker: false);
        }

    [Fact]
        public async Task Transaction_Service_Should_Stay_Available_When_Balance_Worker_Is_Down()
        {
            await _infra.WorkerBalanceFixture.StopAsync();
            await Task.Delay(1000);

            try
            {
                var client = _factory.CreateClient();
                var accountId = Guid.NewGuid();
                var request = new
                {
                    AccountId = accountId,
                    Amount = 100m,
                    Currency = "BRL",
                    Type = 1
                };

                var response = await ResiliencePolicies
                    .GetHttpResiliencePolicy()
                    .ExecuteAsync(() => client.PostAsJsonAsync("/api/transactions", request));

                response.EnsureSuccessStatusCode();

                var redis = CreateConnection(_infra.RedisContainerFixture.ConnectionString);
                var db = redis.GetDatabase();
                var balanceKey = $"balance:{accountId}";

                await Task.Delay(3000);
                var value = await db.StringGetAsync(balanceKey);

                Xunit.Assert.True(value.IsNull);
            }
            finally
            {
                await _infra.WorkerBalanceFixture.StartAsync();
            }
        }

    [Fact]
        public async Task Balance_Should_Catch_Up_After_Worker_Recovers()
        {
            await _infra.WorkerBalanceFixture.StopAsync();

            var accountId = Guid.NewGuid();
            var request = new
            {
                AccountId = accountId,
                Amount = 125m,
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

                var redis = CreateConnection(_infra.RedisContainerFixture.ConnectionString);
                var db = redis.GetDatabase();
                var balanceKey = $"balance:{accountId}";

                var beforeRecovery = await db.StringGetAsync(balanceKey);
                Xunit.Assert.True(beforeRecovery.IsNull);

                await _infra.WorkerBalanceFixture.StartAsync();
                await Task.Delay(1000);

                var afterRecovery = await WaitForBalanceAsync(db, balanceKey);
                Xunit.Assert.False(afterRecovery.IsNull);
            }
            finally
            {
                await _infra.WorkerBalanceFixture.StartAsync();
            }
        }

        public void Dispose()
        {
            _factory.Dispose();
        }

        private static async Task<RedisValue> WaitForBalanceAsync(
            IDatabase db,
            string key,
            int retries = 20)
        {
            for (int i = 0; i < retries; i++)
            {
                var value = await db.StringGetAsync(key);
                if (!value.IsNull)
                {
                    return value;
                }

                await Task.Delay(2000);
            }

            return RedisValue.Null;
        }

        private static ConnectionMultiplexer CreateConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (ConnectionMultiplexer)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(ConnectionMultiplexer.Connect(connection));
            }).GetAwaiter().GetResult();
        }
    }
}
