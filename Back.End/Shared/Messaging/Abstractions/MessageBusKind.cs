namespace Cashflow.Back.End.Shared.Messaging.Abstractions
{
    public enum MessageBusKind
    {
        RabbitMq,
        GooglePubSub,
        AwsSnsSqs,
        AzureServiceBus
    }
}