namespace Cashflow.Shared.Contracts.Idempotency
{
    /// <summary>
    /// Armazenamento de eventos já processados para consumidores idempotentes
    /// Tabela ProcessedEvents: EventId, ConsumerName, ProcessedAt.
    /// </summary>
    public interface IProcessedEventStore
    {
        Task<bool> WasProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
        Task MarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
    }
}