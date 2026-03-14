namespace Cashflow.Shared.Contracts.Api
{
    public sealed record CreateTransactionResponse(
        Guid TransactionId, DateTime CreatedAtUtc);
}