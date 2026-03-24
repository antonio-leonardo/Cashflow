using Cashflow.Shared.Events;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Resilience;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Cashflow.Shared.Messaging.RabbitMQ.MessageBus
{
    public class RabbitMqBus : IMessageBus
    {
        private const string MainExchange = "cashflow.events";
        private const string RetryExchange = "cashflow.events.retry";
        private const string DlqExchange = "cashflow.events.dlq";

        private readonly IChannel _channel;
        private readonly RabbitMqOptions _options;
        private readonly ConcurrentDictionary<string, bool> _topologyCreated = new();

        public RabbitMqBus(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            var connection = CreateConnection(factory);
            _channel = connection.CreateChannelAsync().GetAwaiter().GetResult();

            _channel.ExchangeDeclareAsync(MainExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
            _channel.ExchangeDeclareAsync(RetryExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
            _channel.ExchangeDeclareAsync(DlqExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        }

        private static IConnection CreateConnection(ConnectionFactory factory)
        {
            var policy = ResiliencePolicies.GetResiliencePolicy();

            return (IConnection)policy.ExecuteAsync(() =>
            {
                IConnection connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                return Task.FromResult<object>(connection);
            }).GetAwaiter().GetResult();
        }

        public async Task PublishAsync<TEvent>(
            EventEnvelope<TEvent> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var eventName = typeof(TEvent).Name;

            await EnsureTopologyAsync(eventName, eventName);

            var json = JsonSerializer.Serialize(envelope);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                Persistent = true
            };

            await _channel.BasicPublishAsync(
                exchange: MainExchange,
                routingKey: eventName,
                mandatory: false,
                basicProperties: props,
                body: body);
        }

        public async Task SubscribeAsync<TEvent>(
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var eventName = typeof(TEvent).Name;
            var queueName = string.IsNullOrWhiteSpace(_options.ConsumerName)
                ? eventName
                : $"{_options.ConsumerName}.{eventName}";

            await EnsureTopologyAsync(eventName, queueName);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, args) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(args.Body.ToArray());
                    var envelope = JsonSerializer.Deserialize<EventEnvelope<TEvent>>(json);

                    if (envelope is null)
                    {
                        await _channel.BasicAckAsync(args.DeliveryTag, false);
                        return;
                    }

                    await handler(envelope, cancellationToken);
                    await _channel.BasicAckAsync(args.DeliveryTag, false);
                }
                catch
                {
                    var retryCount = GetRetryCount(args);

                    if (retryCount >= _options.RetryCount)
                    {
                        await PublishToDlqAsync(queueName, args);
                        await _channel.BasicAckAsync(args.DeliveryTag, false);
                        return;
                    }

                    await _channel.BasicRejectAsync(args.DeliveryTag, requeue: false);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer);
        }

        private async Task EnsureTopologyAsync(string routingKey, string queueName)
        {
            if (_topologyCreated.ContainsKey(queueName))
                return;

            var retryQueueName = $"{queueName}.retry";
            var dlqQueueName = $"{queueName}.dlq";

            var mainArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = RetryExchange,
                ["x-dead-letter-routing-key"] = retryQueueName
            };

            var retryArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = MainExchange,
                ["x-dead-letter-routing-key"] = routingKey,
                ["x-message-ttl"] = _options.RetryDelaySeconds * 1000
            };

            await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: mainArgs);
            await _channel.QueueBindAsync(queueName, MainExchange, routingKey: routingKey);

            await _channel.QueueDeclareAsync(retryQueueName, durable: true, exclusive: false, autoDelete: false, arguments: retryArgs);
            await _channel.QueueBindAsync(retryQueueName, RetryExchange, routingKey: retryQueueName);

            await _channel.QueueDeclareAsync(dlqQueueName, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(dlqQueueName, DlqExchange, routingKey: dlqQueueName);

            _topologyCreated.TryAdd(queueName, true);
        }

        private async Task PublishToDlqAsync(string queueName, BasicDeliverEventArgs args)
        {
            var dlqRoutingKey = $"{queueName}.dlq";
            var props = CreateProperties(args.BasicProperties);

            await _channel.BasicPublishAsync(
                exchange: DlqExchange,
                routingKey: dlqRoutingKey,
                mandatory: false,
                basicProperties: props,
                body: args.Body);
        }

        private static BasicProperties CreateProperties(IReadOnlyBasicProperties? source)
        {
            var props = source is null ? new BasicProperties() : new BasicProperties(source);
            props.Persistent = true;
            return props;
        }

        private static long GetRetryCount(BasicDeliverEventArgs args)
        {
            var headers = args.BasicProperties?.Headers;
            if (headers is null || !headers.TryGetValue("x-death", out var death))
            {
                return 0;
            }

            if (death is not IList<object?> deathList)
            {
                return 0;
            }

            long total = 0;

            foreach (var entry in deathList)
            {
                if (entry is not IDictionary<string, object?> deathEntry)
                {
                    continue;
                }

                if (deathEntry.TryGetValue("count", out var countValue) && countValue is not null)
                {
                    total += ConvertToLong(countValue);
                }
            }

            return total;
        }

        private static long ConvertToLong(object value)
        {
            return value switch
            {
                byte b => b,
                sbyte sb => sb,
                short s => s,
                ushort us => us,
                int i => i,
                uint ui => ui,
                long l => l,
                ulong ul => (long)ul,
                byte[] bytes => long.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) ? parsed : 0,
                _ => 0
            };
        }
    }
}
