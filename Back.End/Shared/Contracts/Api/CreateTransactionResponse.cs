namespace Cashflow.Back.End.Shared.Contracts.Api
{
    public sealed record CreateTransactionResponse(
        Guid TransactionId, DateTime CreatedAtUtc);
}