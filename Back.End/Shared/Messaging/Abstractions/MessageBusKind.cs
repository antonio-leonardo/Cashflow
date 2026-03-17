namespace Cashflow.Shared.Messaging.Abstractions
{
    public enum MessageBusKind
    {
        RabbitMq,
        GooglePubSub,
        AwsSnsSqs,
        AzureServiceBus
    }
}