using Cashflow.Back.End.Service.Transaction.Application.Commands;

namespace Cashflow.Back.End.Service.Transaction.Application.Queries
{
    public interface IGetTransactionQueryHandler
    {
        Task<TransactionReadModel?> HandleAsync(GetTransactionQuery query, CancellationToken cancellationToken = default);
    }
}