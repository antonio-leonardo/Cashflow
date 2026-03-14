using Cashflow.Shared.Contracts.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Cashflow.Transaction.Infrastructure.Persistence;

public sealed class ProcessedEventStore : IProcessedEventStore
{
    private readonly IdempotencyDbContext _db;

    public ProcessedEventStore(IdempotencyDbContext db)
    {
        _db = db;
    }

    public async Task<bool> WasProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        return await _db.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId && e.ConsumerName == consumerName, cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        await _db.ProcessedEvents.AddAsync(new ProcessedEventEntity
        {
            EventId = eventId,
            ConsumerName = consumerName,
            ProcessedAt = DateTime.UtcNow
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}