using Cashflow.Transaction.Application.Queries;
using Transaction.Application.Commands;

namespace Transaction.Application.Queries
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