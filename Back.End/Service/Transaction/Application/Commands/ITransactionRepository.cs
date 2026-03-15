namespace Cashflow.Back.End.Service.Transaction.Application.Commands
{
    public interface ITransactionRepository
    {
        Task AddAsync(Domain.Transaction transaction, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<TransactionReadModel?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
    }
}