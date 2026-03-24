namespace Cashflow.Shared.Contracts.Api
{
    public sealed record GetDailyBalanceResponse(
        Guid AccountId,
        DateOnly Date,
        decimal Balance);
}
