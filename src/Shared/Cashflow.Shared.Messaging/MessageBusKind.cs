namespace Cashflow.Shared.Messaging
{
    public enum MessageBusKind
    {
        RabbitMq,
        GooglePubSub,
        AwsSnsSqs,
        AzureServiceBus
    }
}