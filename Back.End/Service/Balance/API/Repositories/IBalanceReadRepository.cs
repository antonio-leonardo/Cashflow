namespace Cashflow.Service.Balance.API.Repositories
{
    public interface IBalanceReadRepository
    {
        Task<DailyBalanceSummary?> GetDailyBalanceAsync(
            Guid accountId,
            DateOnly referenceDate,
            CancellationToken cancellationToken = default);
    }
}
