using Cashflow.Back.End.Shared.Events;
using Cashflow.Back.End.Shared.Messaging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Cashflow.Back.End.Shared.Messaging.Providers.RabbitMQ.MessageBus
{
    public class RabbitMqBus : IMessageBus
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        public RabbitMqBus(IOptions<RabbitMqOptions> options)
        {
            var config = options.Value;

            var factory = new ConnectionFactory
            {
                HostName = config.Host,
                Port = config.Port,
                UserName = config.Username,
                Password = config.Password
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        }

        public async Task PublishAsync<TEvent>(
            EventEnvelope<TEvent> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var queueName = typeof(TEvent).Name;

            await _channel.QueueDeclareAsync(queueName, true, false, false);

            var json = JsonSerializer.Serialize(envelope);
            var body = Encoding.UTF8.GetBytes(json);

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                body: body);
        }

        public async Task SubscribeAsync<TEvent>(
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var queueName = typeof(TEvent).Name;

            await _channel.QueueDeclareAsync(queueName, true, false, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, args) =>
            {
                var json = Encoding.UTF8.GetString(args.Body.ToArray());
                var envelope = JsonSerializer.Deserialize<EventEnvelope<TEvent>>(json);

                if (envelope != null)
                {
                    await handler(envelope, cancellationToken);
                    await _channel.BasicAckAsync(args.DeliveryTag, false);
                }
            };

            await _channel.BasicConsumeAsync(
                queueName,
                autoAck: false,
                consumer: consumer);
        }
    }
}