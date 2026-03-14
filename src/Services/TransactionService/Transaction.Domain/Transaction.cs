using Cashflow.Shared.Events;
using Cashflow.Transaction.Domain.Events;
using Cashflow.Transaction.Domain.ValueObjects;

namespace Cashflow.Transaction.Domain.Entities;

public sealed class Transaction
{
    public TransactionId Id { get; }
    public AccountId AccountId { get; }
    public Money Amount { get; }
    public TransactionType Type { get; }
    public TransactionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; }

    private readonly List<IEvent> _domainEvents = new();
    public IReadOnlyCollection<IEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Transaction(
        TransactionId id,
        AccountId accountId,
        Money amount,
        TransactionType type,
        DateTime createdAtUtc)
    {
        Id = id;
        AccountId = accountId;
        Amount = amount;
        Type = type;
        Status = TransactionStatus.Created;
        CreatedAtUtc = createdAtUtc;

        AddDomainEvent(new TransactionCreatedEventV1(
            Id.Value,
            AccountId.Value,
            Amount.Value,
            Amount.Currency));
    }

    public static Transaction Create(
        Guid transactionId,
        Guid accountId,
        decimal amount,
        string currency,
        TransactionType type,
        DateTime? createdAtUtc = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        var money = new Money(amount, currency);
        return new Transaction(
            new TransactionId(transactionId),
            new AccountId(accountId),
            money,
            type,
            createdAtUtc ?? DateTime.UtcNow);
    }

    private void AddDomainEvent(IEvent @event) => _domainEvents.Add(@event);
}