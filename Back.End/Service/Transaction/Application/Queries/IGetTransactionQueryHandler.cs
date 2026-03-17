using Cashflow.Service.Transaction.Application.Commands;

namespace Cashflow.Service.Transaction.Application.Queries
{
    public interface IGetTransactionQueryHandler
    {
        Task<TransactionReadModel?> HandleAsync(GetTransactionQuery query, CancellationToken cancellationToken = default);
    }
}