using Cashflow.Back.End.Shared.NoSql.Abstractions;
using MongoDB.Driver;

namespace Cashflow.Shared.NoSql.Providers.MongoDB
{
    public class MongoQueryRepository<T> : INoSqlQueryRepository<T>
    {
        protected readonly IMongoCollection<T> _collection;

        public MongoQueryRepository(IMongoDatabase database, string collectionName)
        {
            _collection = database.GetCollection<T>(collectionName);
        }

        public async Task<T?> GetAsync(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> QueryAsync(Func<T, bool> predicate)
        {
            return _collection.AsQueryable().Where(predicate).ToList();
        }
    }
}