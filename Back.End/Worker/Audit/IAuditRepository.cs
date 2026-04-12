using Cashflow.Service.Transaction.Domain;

namespace Cashflow.Worker.Audit
{
    public interface IAuditRepository
    {
        /// <summary>
        /// Persists an audit record for the given event.
        /// Implementations must be idempotent: a duplicate event must be silently ignored.
        /// </summary>
        Task RecordAsync(TransactionCreatedEventV1 evt, CancellationToken cancellationToken = default);
    }
}
