using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.Redis;
using StackExchange.Redis;
using System.Globalization;

namespace Cashflow.Worker.Balance
{
    public class RedisBalanceRepository : RedisCommandRepository<TransactionCreatedEventV1>, IBalanceProjectionRepository
    {
        private const string BalanceKeyPrefix = "balance:";
        private const string DailyBalanceKeyPrefix = "balance:daily:";
        private const string ProcessedKeyPrefix = "processed:";

        private static readonly RedisValue CreditsField = "credits";
        private static readonly RedisValue DebitsField  = "debits";
        private static readonly RedisValue NetField      = "net";
        private const string AtomicIdempotentBalanceScript = @"
local processedKey = KEYS[1]
local totalKey = KEYS[2]
local dailyKey = KEYS[3]
local ttlSeconds = ARGV[1]
local creditField = ARGV[2]
local debitField = ARGV[3]
local netField = ARGV[4]
local amount = ARGV[5]
local signedAmount = ARGV[6]
local isCredit = ARGV[7]

local acquired = redis.call('SET', processedKey, '1', 'EX', ttlSeconds, 'NX')
if not acquired then
  return 0
end

redis.call('INCRBYFLOAT', totalKey, signedAmount)
redis.call('HINCRBYFLOAT', dailyKey, netField, signedAmount)
if isCredit == '1' then
  redis.call('HINCRBYFLOAT', dailyKey, creditField, amount)
  redis.call('HSETNX', dailyKey, debitField, '0')
else
  redis.call('HINCRBYFLOAT', dailyKey, debitField, amount)
  redis.call('HSETNX', dailyKey, creditField, '0')
end
return 1";

        public RedisBalanceRepository(IConnectionMultiplexer redis) : base(redis)
        {
        }

        public async Task<bool> ApplyAsync(
            TransactionCreatedEventV1 evt,
            string consumerName,
            string idempotencyKey,
            TimeSpan processedEventTtl)
        {
            var isCredit = evt.Type == TransactionType.Credit;
            var signedAmount = isCredit ? evt.Amount : -evt.Amount;
            var normalizedIdempotencyKey = idempotencyKey.Trim().ToLowerInvariant();
            var processedKey = $"{ProcessedKeyPrefix}{consumerName}:{normalizedIdempotencyKey}";

            var totalBalanceKey = $"{BalanceKeyPrefix}{evt.AccountId}";
            var dailyBalanceKey = $"{DailyBalanceKeyPrefix}{evt.AccountId}:{DateOnly.FromDateTime(evt.OccurredAt):yyyy-MM-dd}";
            var ttlSeconds = Math.Max(1, (int)Math.Ceiling(processedEventTtl.TotalSeconds));

            // Executes dedupe + balance updates atomically, preventing double-apply on re-delivery.
            var scriptResult = await _db.ScriptEvaluateAsync(
                AtomicIdempotentBalanceScript,
                new RedisKey[] { processedKey, totalBalanceKey, dailyBalanceKey },
                new RedisValue[]
                {
                    ttlSeconds.ToString(CultureInfo.InvariantCulture),
                    CreditsField,
                    DebitsField,
                    NetField,
                    evt.Amount.ToString(CultureInfo.InvariantCulture),
                    signedAmount.ToString(CultureInfo.InvariantCulture),
                    isCredit ? "1" : "0"
                });

            return long.TryParse(scriptResult.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var appliedFlag)
                && appliedFlag == 1L;
        }
    }
}
