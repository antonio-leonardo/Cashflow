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
            Guid correlationId = default,
            string? traceParent = null,
            string? baggage = null,
            Guid eventId = default,
            DateTime occurredAt = default,
            int version = 1,
            string? eventType = null)
            : base(
                  eventId: eventId == default ? null : eventId,
                  correlationId: correlationId == default ? null : correlationId,
                  occurredAt: occurredAt == default ? null : occurredAt,
                  version: version,
                  eventType: string.IsNullOrWhiteSpace(eventType) ? null : eventType,
                  traceParent: traceParent,
                  baggage: baggage)
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
