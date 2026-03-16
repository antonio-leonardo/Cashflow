namespace Cashflow.Back.End.Shared.NoSql.Abstractions
{
    public interface INoSqlCommandRepository<T>
    {
        Task InsertAsync(T entity);
    }
}