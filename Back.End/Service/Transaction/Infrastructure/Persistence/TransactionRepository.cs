using Cashflow.Back.End.Service.Transaction.Application.Commands;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cashflow.Back.End.Service.Transaction.Infrastructure.Persistence;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly TransactionDbContext _db;

    public TransactionRepository(TransactionDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Domain.Transaction transaction, CancellationToken cancellationToken = default)
    {
        var entity = new TransactionEntity
        {
            Id = transaction.Id.Value,
            AccountId = transaction.AccountId.Value,
            Amount = transaction.Amount.Value,
            Currency = transaction.Amount.Currency,
            Type = (int)transaction.Type,
            Status = (int)transaction.Status,
            CreatedAtUtc = transaction.CreatedAtUtc
        };
        await _db.Transactions.AddAsync(entity, cancellationToken);

        foreach (var domainEvent in transaction.DomainEvents)
        {
            var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
            var outbox = new OutboxEventEntity
            {
                EventId = domainEvent.EventId,
                EventType = domainEvent.EventType,
                Payload = payload,
                CreatedAt = domainEvent.OccurredAt
            };
            await _db.OutboxEvents.AddAsync(outbox, cancellationToken);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TransactionReadModel?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (entity is null) return null;
        return new TransactionReadModel(
            entity.Id,
            entity.AccountId,
            entity.Amount,
            entity.Currency,
            entity.Type,
            entity.Status,
            entity.CreatedAtUtc);
    }
}