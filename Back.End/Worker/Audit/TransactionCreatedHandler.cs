using Cashflow.Service.Transaction.Domain;

namespace Cashflow.Worker.Audit
{
    public class TransactionCreatedHandler
    {
        private readonly IAuditRepository _repository;

        public TransactionCreatedHandler(IAuditRepository repository)
        {
            _repository = repository;
        }

        public Task HandleAsync(TransactionCreatedEventV1 evt, CancellationToken ct)
            => _repository.RecordAsync(evt, ct);
    }
}