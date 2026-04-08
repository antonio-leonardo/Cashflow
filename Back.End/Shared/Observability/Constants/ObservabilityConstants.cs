namespace Cashflow.Shared.Observability
{
    public static class ObservabilityConstants
    {
        public const string CorrelationIdHeaderName = "X-Correlation-Id";
        public const string MessagingActivitySourceName = "Cashflow.Messaging";
        public const string MessagingMeterName = "Cashflow.Messaging";
        public const string BusinessActivitySourceName = "Cashflow.Business";
        public const string BusinessMeterName = "Cashflow.Business";
    }
}
