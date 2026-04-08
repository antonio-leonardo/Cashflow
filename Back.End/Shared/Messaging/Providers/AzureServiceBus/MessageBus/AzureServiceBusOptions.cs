namespace Cashflow.Shared.Messaging.AzureServiceBus.MessageBus
{
    public class AzureServiceBusOptions
    {
        public string? ConnectionString { get; set; }
        public string? Namespace { get; set; }
        public bool UseManagedIdentity { get; set; }
        public string ConsumerName { get; set; } = string.Empty;
        public int MaxConcurrentCalls { get; set; } = 1;
        public int MaxDeliveryCount { get; set; } = 5;
    }
}
