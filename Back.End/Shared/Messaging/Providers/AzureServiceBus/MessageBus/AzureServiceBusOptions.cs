namespace Cashflow.Shared.Messaging.AzureServiceBus.MessageBus
{
    public class AzureServiceBusOptions
    {
        public string? ConnectionString { get; set; }
        public string? CustomEndpointAddress { get; set; }
        public string? Namespace { get; set; }
        public bool UseManagedIdentity { get; set; }
        public string ConsumerName { get; set; } = string.Empty;
        public int MaxConcurrentCalls { get; set; } = 1;
        public int PrefetchCount { get; set; } = 0;

        /// <summary>
        /// Seconds to keep renewing the message lock during processing.
        /// Defaults to 5 minutes. Set to 0 to disable auto-renewal.
        /// NOTE: MaxDeliveryCount is a server-side property of the queue/subscription entity
        /// and cannot be configured from the client SDK. Configure it via ARM/bicep/portal.
        /// </summary>
        public int MaxAutoLockRenewalSeconds { get; set; } = 300;

        /// <summary>
        /// When true, the processor uses ServiceBusSessionProcessor so that
        /// session-enabled queues/subscriptions are consumed in order per session.
        /// </summary>
        public bool EnableSessions { get; set; } = false;
    }
}
