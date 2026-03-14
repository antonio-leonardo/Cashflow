namespace Transaction.Application.Commands
{
    public interface ITransactionRepository
    {
        Task AddAsync(Cashflow.Transaction.Domain.Entities.Transaction transaction, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<TransactionReadModel?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
    }
}