using StackExchange.Redis;
using System.Globalization;

namespace Cashflow.Service.Balance.API.Repositories
{
    public sealed class RedisDailyBalanceRepository
    {
        private const string DailyBalanceKeyPrefix = "balance:daily:";
        private readonly IDatabase _database;

        public RedisDailyBalanceRepository(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        public async Task<decimal?> GetDailyBalanceAsync(
            Guid accountId,
            DateOnly referenceDate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = BuildDailyKey(accountId, referenceDate);
            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
            {
                return null;
            }

            var rawValue = value.ToString();
            if (TryParseDecimal(rawValue, out var parsedValue))
            {
                return parsedValue;
            }

            throw new FormatException(
                $"Invalid daily balance format found in Redis for key '{key}'.");
        }

        private static string BuildDailyKey(Guid accountId, DateOnly date)
        {
            return $"{DailyBalanceKeyPrefix}{accountId}:{date:yyyy-MM-dd}";
        }

        private static bool TryParseDecimal(string rawValue, out decimal parsedValue)
        {
            return decimal.TryParse(
                rawValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsedValue)
                || decimal.TryParse(
                    rawValue,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out parsedValue);
        }
    }
}
