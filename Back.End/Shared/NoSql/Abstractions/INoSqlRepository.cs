namespace Cashflow.Back.End.Shared.NoSql.Abstractions
{
    public interface INoSqlRepository<T>
    {
        Task InsertAsync(T entity);

        Task<T?> GetAsync(string id);

        Task<IEnumerable<T>> QueryAsync(Func<T, bool> predicate);
    }
}