namespace Cashflow.Transaction.Infrastructure.Persistence;

public sealed class ProcessedEventEntity
{
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}