using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.Redis;
using StackExchange.Redis;
using System.Globalization;

namespace Cashflow.Worker.Balance
{
    public class RedisBalanceRepository : RedisCommandRepository<TransactionCreatedEventV1>
    {
        private const string BalanceKeyPrefix = "balance:";
        private const string DailyBalanceKeyPrefix = "balance:daily:";
        private const string BalanceLockKeyPrefix = "lock:balance:";

        private static readonly RedisValue CreditsField = "credits";
        private static readonly RedisValue DebitsField  = "debits";
        private static readonly RedisValue NetField      = "net";
        private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(50);
        private const int MaxLockAttempts = 20;

        public RedisBalanceRepository(IConnectionMultiplexer redis) : base(redis)
        {
        }

        public async Task ApplyAsync(TransactionCreatedEventV1 evt)
        {
            await ExecuteWithAccountLockAsync(evt.AccountId, async () =>
            {
                var isCredit = evt.Type == TransactionType.Credit;
                var signedAmount = isCredit ? evt.Amount : -evt.Amount;

                var totalBalanceKey = $"{BalanceKeyPrefix}{evt.AccountId}";
                var dailyBalanceKey = $"{DailyBalanceKeyPrefix}{evt.AccountId}:{DateOnly.FromDateTime(evt.OccurredAt):yyyy-MM-dd}";

                await IncrementNetStringAsync(totalBalanceKey, signedAmount);
                await IncrementDailyHashAsync(dailyBalanceKey, evt.Amount, isCredit);
            });
        }

        private async Task ExecuteWithAccountLockAsync(Guid accountId, Func<Task> action)
        {
            var lockKey = $"{BalanceLockKeyPrefix}{accountId}";
            var lockToken = Guid.NewGuid().ToString("N");

            for (var attempt = 1; attempt <= MaxLockAttempts; attempt++)
            {
                var lockTaken = await _db.LockTakeAsync(lockKey, lockToken, LockTtl);
                if (lockTaken)
                {
                    try
                    {
                        await action();
                        return;
                    }
                    finally
                    {
                        await _db.LockReleaseAsync(lockKey, lockToken);
                    }
                }

                await Task.Delay(LockRetryDelay);
            }

            throw new TimeoutException($"Could not acquire Redis lock for account '{accountId}'.");
        }

        private async Task IncrementNetStringAsync(string key, decimal signedAmount)
        {
            var value = await _db.StringGetAsync(key);
            var current = value.HasValue && TryParseDecimal(value.ToString(), out var parsed) ? parsed : 0m;
            await _db.StringSetAsync(key, (current + signedAmount).ToString(CultureInfo.InvariantCulture));
        }

        private async Task IncrementDailyHashAsync(string key, decimal amount, bool isCredit)
        {
            var entries = await _db.HashGetAsync(key, new RedisValue[] { CreditsField, DebitsField, NetField });

            var credits = ParseRedisDecimal(entries[0]);
            var debits  = ParseRedisDecimal(entries[1]);
            var net     = ParseRedisDecimal(entries[2]);

            if (isCredit)
            {
                credits += amount;
                net     += amount;
            }
            else
            {
                debits += amount;
                net    -= amount;
            }

            await _db.HashSetAsync(key, new HashEntry[]
            {
                new(CreditsField, credits.ToString(CultureInfo.InvariantCulture)),
                new(DebitsField,  debits.ToString(CultureInfo.InvariantCulture)),
                new(NetField,     net.ToString(CultureInfo.InvariantCulture))
            });
        }

        private static decimal ParseRedisDecimal(RedisValue value)
        {
            if (!value.HasValue) return 0m;
            return TryParseDecimal(value.ToString(), out var result) ? result : 0m;
        }

        private static bool TryParseDecimal(string rawValue, out decimal parsedValue)
        {
            return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
                || decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedValue);
        }
    }
}
