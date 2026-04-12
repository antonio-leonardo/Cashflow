using Cashflow.Service.Transaction.Domain;

namespace Cashflow.Worker.Report
{
    public interface IReportRepository
    {
        /// <summary>
        /// Appends a transaction to the report projection.
        /// Implementations must be idempotent: a duplicate event must be silently ignored.
        /// </summary>
        Task AppendAsync(TransactionCreatedEventV1 evt, CancellationToken cancellationToken = default);
    }
}
