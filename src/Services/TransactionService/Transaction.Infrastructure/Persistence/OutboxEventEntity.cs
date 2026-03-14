namespace Cashflow.Transaction.Infrastructure.Persistence
{
    public sealed class OutboxEventEntity
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}