namespace Cashflow.Shared.NoSql.Abstractions
{
    public interface INoSqlCommandRepository<in T>
    {
        Task InsertAsync(T entity);
    }
}
