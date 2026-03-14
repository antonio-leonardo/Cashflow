using Cashflow.Transaction.Application.Queries;
using Transaction.Application.Commands;

namespace Transaction.Application.Queries
{
    public interface IGetTransactionQueryHandler
    {
        Task<TransactionReadModel?> HandleAsync(GetTransactionQuery query, CancellationToken cancellationToken = default);
    }
}