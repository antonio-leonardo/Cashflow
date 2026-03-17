using Cashflow.Service.Transaction.Application.Commands;

namespace Cashflow.Service.Transaction.Application.Queries
{
    public sealed class GetTransactionQueryHandler : IGetTransactionQueryHandler
    {
        private readonly ITransactionRepository _repository;

        public GetTransactionQueryHandler(ITransactionRepository repository)
        {
            _repository = repository;
        }

        public Task<TransactionReadModel?> HandleAsync(GetTransactionQuery query, CancellationToken cancellationToken = default)
            => _repository.GetByIdAsync(query.TransactionId, cancellationToken);
    }
}