using Cashflow.Shared.Events;

namespace Cashflow.Service.Transaction.Domain
{
    public sealed class TransactionCreatedEventV1 : EventBase
    {
        public TransactionCreatedEventV1(
            Guid transactionId,
            Guid accountId,
            decimal amount,
            string currency,
            TransactionType type)
            : base(version: 1)
        {
            TransactionId = transactionId;
            AccountId = accountId;
            Amount = amount;
            Currency = currency;
            Type = type;
        }

        public Guid TransactionId { get; }
        public Guid AccountId { get; }
        public decimal Amount { get; }
        public string Currency { get; }
        public TransactionType Type { get; }
    }
}