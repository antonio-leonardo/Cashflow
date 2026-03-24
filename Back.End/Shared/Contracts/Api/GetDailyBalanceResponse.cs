namespace Cashflow.Shared.Contracts.Api
{
    public sealed record GetDailyBalanceResponse(
        Guid AccountId,
        DateOnly Date,
        decimal TotalCredits,
        decimal TotalDebits,
        decimal NetBalance);
}
