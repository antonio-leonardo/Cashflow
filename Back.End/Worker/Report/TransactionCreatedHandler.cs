using Cashflow.Service.Transaction.Domain;

namespace Cashflow.Worker.Report
{
    public class TransactionCreatedHandler
    {
        private readonly IReportRepository _repository;

        public TransactionCreatedHandler(IReportRepository repository)
        {
            _repository = repository;
        }

        public Task HandleAsync(TransactionCreatedEventV1 evt, CancellationToken ct)
            => _repository.AppendAsync(evt, ct);
    }
}