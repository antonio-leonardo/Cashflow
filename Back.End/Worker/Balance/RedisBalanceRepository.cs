using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.NoSql.Redis;
using StackExchange.Redis;

namespace Cashflow.Worker.Balance
{
    public class RedisBalanceRepository : RedisCommandRepository<TransactionCreatedEventV1>
    {
        public RedisBalanceRepository(IConnectionMultiplexer redis) : base(redis)
        {
        }

        public async Task ApplyAsync(TransactionCreatedEventV1 evt)
        {
            var key = $"balance:{evt.AccountId}";

            var value = await _db.StringGetAsync(key);

            decimal balance = value.HasValue ? decimal.Parse(value!.ToString()) : 0;

            balance += evt.Amount;

            await _db.StringSetAsync(key, balance.ToString());
        }
    }
}