namespace Cashflow.Shared.Observability
{
    public static class ObservabilityConstants
    {
        public const string CorrelationIdHeaderName = "X-Correlation-Id";
        public const string MessagingActivitySourceName = "Cashflow.Messaging.RabbitMQ";
        public const string MessagingMeterName = "Cashflow.Messaging.RabbitMQ";
    }
}
