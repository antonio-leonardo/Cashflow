using StackExchange.Redis;
using System.Globalization;

namespace Cashflow.Service.Balance.API.Repositories
{
    public sealed class RedisDailyBalanceRepository : IBalanceReadRepository
    {
        private const string DailyBalanceKeyPrefix = "balance:daily:";

        private static readonly RedisValue CreditsField = "credits";
        private static readonly RedisValue DebitsField  = "debits";
        private static readonly RedisValue NetField      = "net";

        private readonly IDatabase _database;

        public RedisDailyBalanceRepository(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        public async Task<DailyBalanceSummary?> GetDailyBalanceAsync(
            Guid accountId,
            DateOnly referenceDate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key     = BuildDailyKey(accountId, referenceDate);
            var entries = await _database.HashGetAsync(key, new RedisValue[] { CreditsField, DebitsField, NetField });

            if (!entries[0].HasValue && !entries[1].HasValue && !entries[2].HasValue)
            {
                return null;
            }

            return new DailyBalanceSummary(
                TotalCredits: ParseDecimal(entries[0]),
                TotalDebits:  ParseDecimal(entries[1]),
                NetBalance:   ParseDecimal(entries[2]));
        }

        private static string BuildDailyKey(Guid accountId, DateOnly date)
        {
            return $"{DailyBalanceKeyPrefix}{accountId}:{date:yyyy-MM-dd}";
        }

        private static decimal ParseDecimal(RedisValue value)
        {
            if (!value.HasValue) return 0m;

            var raw = value.ToString();

            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out result))
                return result;

            throw new FormatException($"Invalid decimal format in Redis: '{raw}'.");
        }
    }

    public sealed record DailyBalanceSummary(
        decimal TotalCredits,
        decimal TotalDebits,
        decimal NetBalance);
}
