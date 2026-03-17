using StackExchange.Redis;
using System.Text.Json;

namespace Cashflow.Shared.NoSql.Redis
{
    public class RedisQueryRepository<T>
    {
        protected readonly IDatabase _db;

        public RedisQueryRepository(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task<T?> GetAsync(string key)
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue) return default;

            return JsonSerializer.Deserialize<T>(value!.ToString());
        }
    }
}