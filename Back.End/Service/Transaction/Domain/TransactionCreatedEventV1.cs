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
            TransactionType type,
            Guid? correlationId = null,
            string? traceParent = null,
            string? baggage = null)
            : base(correlationId: correlationId, version: 1, traceParent: traceParent, baggage: baggage)
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
