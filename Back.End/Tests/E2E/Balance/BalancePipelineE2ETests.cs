using Infrastructure.Test;
using StackExchange.Redis;
using System.Net.Http.Json;

namespace E2E.Balance.Tests
{
    [Collection("CompleteInfrastructureCollection")]
    public class BalancePipelineE2ETests
    {
        private readonly BalanceCompleteInfrastructureFixture _infra;
        private readonly CustomWebApplicationFactory _factory;

        public BalancePipelineE2ETests(BalanceCompleteInfrastructureFixture infra)
        {
            _infra = infra;
            _factory = new CustomWebApplicationFactory(_infra);
        }

        [Fact]
        public async Task Transaction_Should_Update_ReadModels()
        {
            var client = _factory.CreateClient();

            var objectToRequest = new
            {
                AccountId = Guid.NewGuid(),
                Amount = 100,
                Currency = "BRL",
                Type = 1
            };

            var response = await client.PostAsJsonAsync("/api/transactions", objectToRequest);

            response.EnsureSuccessStatusCode();

            await Task.Delay(8000);

            var redis = await ConnectionMultiplexer.ConnectAsync(_infra.RedisContainerFixture.ConnectionString);
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync($"balance:{objectToRequest.AccountId}");

            if (value.IsNull)
            {
                var retries = 10;
                var success = false;

                for (int i = 0; i < retries; i++)
                {
                    value = await db.StringGetAsync($"balance:{objectToRequest.AccountId}");

                    if (!value.IsNull)
                    {
                        success = true;
                        break;
                    }

                    await Task.Delay(500);
                }
            }

            Assert.False(value.IsNull);
        }
    }
}