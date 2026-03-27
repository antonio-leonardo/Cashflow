using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Resilience;
using Infrastructure.Test;
using RabbitMQ.Client;
using StackExchange.Redis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace E2E.Balance.Tests
{
    [Collection("CompleteInfrastructureCollection")]
    public class BalancePipelineE2ETests : IDisposable
    {
        private const string EventsExchange = "cashflow.events";

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
            var balanceKey = $"balance:{objectToRequest.AccountId}";
            var dailyBalanceKey = $"balance:daily:{objectToRequest.AccountId}:{referenceDate:yyyy-MM-dd}";

            var totalBalance = await WaitForStringValueAsync(db, balanceKey, retries: 10, delayMs: 5000);
            var dailyHashEntries = await WaitForHashEntriesAsync(db, dailyBalanceKey, retries: 10, delayMs: 5000);

            Xunit.Assert.False(totalBalance.IsNull);
            Xunit.Assert.NotEmpty(dailyHashEntries);
        }

        [Fact]
        public async Task Duplicate_EventId_Redelivery_Should_Be_Applied_Only_Once()
        {
            await _infra.WorkerBalanceFixture.StartAsync();
            await Task.Delay(1000);

            var redis = CreateConnection(_infra.RedisContainerFixture.ConnectionString);
            var db = redis.GetDatabase();

            var evt = new TransactionCreatedEventV1(
                transactionId: Guid.NewGuid(),
                accountId: Guid.NewGuid(),
                amount: 250m,
                currency: "BRL",
                type: TransactionType.Credit);

            var envelope = new EventEnvelope<TransactionCreatedEventV1>(
                evt,
                new MessageMetadata(
                    CorrelationId: Guid.NewGuid().ToString(),
                    CausationId: evt.EventId.ToString(),
                    Source: nameof(BalancePipelineE2ETests),
                    TenantId: null,
                    CreatedAtUtc: DateTime.UtcNow));

            await PublishTransactionCreatedAsync(envelope);
            await PublishTransactionCreatedAsync(envelope);

            var totalBalanceKey = $"balance:{evt.AccountId}";
            var dailyBalanceKey = $"balance:daily:{evt.AccountId}:{DateOnly.FromDateTime(evt.OccurredAt):yyyy-MM-dd}";
            var processedKey = $"processed:balance-worker:{evt.EventId.ToString("N")}";

            var totalBalance = await WaitForStringValueAsync(db, totalBalanceKey);
            var dailyHashEntries = await WaitForHashEntriesAsync(db, dailyBalanceKey);
            var processedMarker = await WaitForStringValueAsync(db, processedKey);

            Xunit.Assert.False(totalBalance.IsNull);
            Xunit.Assert.Equal(evt.Amount, ParseDecimal(totalBalance));

            Xunit.Assert.NotEmpty(dailyHashEntries);
            Xunit.Assert.Equal(evt.Amount, GetHashDecimalField(dailyHashEntries, "credits"));
            Xunit.Assert.Equal(0m, GetHashDecimalField(dailyHashEntries, "debits"));
            Xunit.Assert.Equal(evt.Amount, GetHashDecimalField(dailyHashEntries, "net"));

            Xunit.Assert.False(processedMarker.IsNull);
            Xunit.Assert.Equal("1", processedMarker.ToString());
        }

        private ConnectionMultiplexer CreateConnection(string connection)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();
            return (ConnectionMultiplexer)policy.ExecuteAsync(() =>
            {
                return Task.FromResult<object>(ConnectionMultiplexer.Connect(connection));
            }).GetAwaiter().GetResult();
        }

        private async Task PublishTransactionCreatedAsync(EventEnvelope<TransactionCreatedEventV1> envelope)
        {
            var options = _infra.RabbitMqContainerFixture.RabbitMqOptions;
            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(
                exchange: EventsExchange,
                type: ExchangeType.Direct,
                durable: true);

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: EventsExchange,
                routingKey: nameof(TransactionCreatedEventV1),
                mandatory: false,
                basicProperties: properties,
                body: payload);
        }

        private static async Task<RedisValue> WaitForStringValueAsync(
            IDatabase db,
            string key,
            int retries = 20,
            int delayMs = 500)
        {
            for (var attempt = 0; attempt < retries; attempt++)
            {
                var value = await db.StringGetAsync(key);
                if (!value.IsNull)
                {
                    return value;
                }

                await Task.Delay(delayMs);
            }

            return RedisValue.Null;
        }

        private static async Task<HashEntry[]> WaitForHashEntriesAsync(
            IDatabase db,
            string key,
            int retries = 20,
            int delayMs = 500)
        {
            for (var attempt = 0; attempt < retries; attempt++)
            {
                var entries = await db.HashGetAllAsync(key);
                if (entries.Length > 0)
                {
                    return entries;
                }

                await Task.Delay(delayMs);
            }

            return [];
        }

        private static decimal GetHashDecimalField(HashEntry[] entries, string fieldName)
        {
            foreach (var entry in entries)
            {
                if (entry.Name == fieldName)
                {
                    return ParseDecimal(entry.Value);
                }
            }

            Xunit.Assert.Fail($"Expected hash field '{fieldName}' to exist.");
            return 0m;
        }

        private static decimal ParseDecimal(RedisValue value)
        {
            return decimal.Parse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            _factory.Dispose();
        }
    }
}
