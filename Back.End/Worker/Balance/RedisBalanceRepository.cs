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

        public RedisBalanceRepository(IConnectionMultiplexer redis) : base(redis)
        {
        }

        public async Task ApplyAsync(TransactionCreatedEventV1 evt)
        {
            var totalBalanceKey = $"{BalanceKeyPrefix}{evt.AccountId}";
            var dailyBalanceKey = $"{DailyBalanceKeyPrefix}{evt.AccountId}:{DateOnly.FromDateTime(evt.OccurredAt):yyyy-MM-dd}";

            await IncrementBalanceAsync(totalBalanceKey, evt.Amount);
            await IncrementBalanceAsync(dailyBalanceKey, evt.Amount);
        }

        private async Task IncrementBalanceAsync(string key, decimal amount)
        {
            var value = await _db.StringGetAsync(key);
            var currentBalance = value.HasValue && TryParseDecimal(value.ToString(), out var parsedBalance)
                ? parsedBalance
                : 0m;

            currentBalance += amount;

            await _db.StringSetAsync(key, currentBalance.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryParseDecimal(string rawValue, out decimal parsedValue)
        {
            return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
                || decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedValue);
        }
    }
}
