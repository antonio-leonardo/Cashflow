using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Cashflow.Shared.Observability
{
    public static class CashflowBusinessTelemetry
    {
        private static readonly Meter BusinessMeter = new(ObservabilityConstants.BusinessMeterName);
        private static readonly Counter<long> TransactionsCreatedCounter =
            BusinessMeter.CreateCounter<long>("cashflow.business.transactions.created");

        public static readonly ActivitySource ActivitySource =
            new(ObservabilityConstants.BusinessActivitySourceName);

        public static Activity? StartCreateTransactionActivity(
            Guid transactionId,
            Guid accountId,
            string currency,
            string transactionType)
        {
            var activity = ActivitySource.StartActivity("transaction create", ActivityKind.Internal);
            activity?.SetTag("cashflow.transaction.id", transactionId);
            activity?.SetTag("cashflow.account.id", accountId);
            activity?.SetTag("cashflow.transaction.currency", currency);
            activity?.SetTag("cashflow.transaction.type", transactionType);
            return activity;
        }

        public static void RecordTransactionCreated(string currency, string transactionType)
        {
            TransactionsCreatedCounter.Add(1,
                new KeyValuePair<string, object?>("cashflow.transaction.currency", currency),
                new KeyValuePair<string, object?>("cashflow.transaction.type", transactionType));
        }
    }
}
