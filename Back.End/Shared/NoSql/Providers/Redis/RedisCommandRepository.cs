using StackExchange.Redis;
using System.Text.Json;

namespace Cashflow.Shared.NoSql.Providers.Redis
{
    public class RedisCommandRepository<T>
    {
        protected readonly IDatabase _db;

        public RedisCommandRepository(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task SetAsync(string key, T value)
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, json);
        }
    }
}