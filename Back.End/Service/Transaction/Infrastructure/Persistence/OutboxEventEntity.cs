namespace Cashflow.Back.End.Service.Transaction.Infrastructure.Persistence
{
    public sealed class OutboxEventEntity
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int RetryCount { get; set; }
        public string? Error { get; set; }
    }
}