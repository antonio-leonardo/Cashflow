namespace Cashflow.Shared.NoSql.Abstractions
{
    public interface INoSqlCommandRepository<T>
    {
        Task InsertAsync(T entity);
    }
}