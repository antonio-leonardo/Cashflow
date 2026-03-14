namespace Cashflow.Shared.Messaging;

using Cashflow.Shared.Events;

/// <summary>
/// Implementação stub de IMessageBus para workers: Publish no-op, Subscribe bloqueia até cancelamento.
/// Substituir por RabbitMqBus / GooglePubSubBus / etc. quando mensageria real estiver configurada.
/// </summary>
public sealed class StubMessageBus : IMessageBus
{
    public Task PublishAsync<TEvent>(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
        where TEvent : IEvent
        => Task.CompletedTask;

    public async Task SubscribeAsync<TEvent>(
        Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
