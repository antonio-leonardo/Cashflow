using Cashflow.Back.End.Shared.NoSql.Abstractions;
using MongoDB.Driver;

namespace Cashflow.Shared.NoSql.Providers.MongoDB
{
    public class MongoCommandRepository<T> : INoSqlCommandRepository<T>
    {
        protected readonly IMongoCollection<T> _collection;

        public MongoCommandRepository(IMongoDatabase database, string collectionName)
        {
            _collection = database.GetCollection<T>(collectionName);
        }

        public async Task InsertAsync(T entity)
        {
            await _collection.InsertOneAsync(entity);
        }
    }
}