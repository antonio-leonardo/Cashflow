using Cashflow.Service.Transaction.Domain;

namespace Cashflow.Service.Transaction.Application.Commands
{
    public sealed record CreateTransactionCommand(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    TransactionType Type,
    string CorrelationId,
    string? UserId);

    public sealed record TransactionReadModel(
        Guid Id,
        Guid AccountId,
        decimal Amount,
        string Currency,
        int Type,
        int Status,
        DateTime CreatedAtUtc);
}