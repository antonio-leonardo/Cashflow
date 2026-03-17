namespace Cashflow.Shared.NoSql.Abstractions
{
    public interface INoSqlQueryRepository<T>
    {
        Task<T?> GetAsync(string id);

        Task<IEnumerable<T>> QueryAsync(Func<T, bool> predicate);
    }
}