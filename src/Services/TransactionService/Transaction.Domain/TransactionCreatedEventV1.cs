using Cashflow.Shared.Events;

namespace Cashflow.Transaction.Domain.Events;

public sealed class TransactionCreatedEventV1 : EventBase
{
    public TransactionCreatedEventV1(
        Guid transactionId,
        Guid accountId,
        decimal amount,
        string currency)
        : base(version: 1)
    {
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
    }

    public Guid TransactionId { get; }
    public Guid AccountId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
}
