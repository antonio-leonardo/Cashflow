namespace Cashflow.Service.Transaction.Domain
{
    public readonly record struct TransactionId(Guid Value);

    public readonly record struct AccountId(Guid Value);

    public sealed record Money(decimal Value, string Currency);

    public enum TransactionType
    {
        Credit,
        Debit
    }

    public enum TransactionStatus
    {
        Created,
        Completed,
        Failed
    }
}