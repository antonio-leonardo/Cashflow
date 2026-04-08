using Cashflow.Shared.Events;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Resilience;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;

namespace Cashflow.Shared.Messaging.RabbitMQ.MessageBus
{
    public class RabbitMqBus : IMessageBus
    {
        private const string MainExchange = "cashflow.events";
        private const string RetryExchange = "cashflow.events.retry";
        private const string DlqExchange = "cashflow.events.dlq";

        private const string CorrelationIdHeader = "x-correlation-id";
        private const string CausationIdHeader = "x-causation-id";
        private const string TraceParentHeader = "traceparent";
        private const string BaggageHeader = "baggage";

        private static readonly ActivitySource MessagingActivitySource = new("Cashflow.Messaging");
        private static readonly Meter MessagingMeter = new("Cashflow.Messaging");
        private static readonly Counter<long> PublishedMessagesCounter =
            MessagingMeter.CreateCounter<long>("cashflow.messaging.rabbitmq.published");
        private static readonly Counter<long> ConsumedMessagesCounter =
            MessagingMeter.CreateCounter<long>("cashflow.messaging.rabbitmq.consumed");
        private static readonly Counter<long> FailedMessagesCounter =
            MessagingMeter.CreateCounter<long>("cashflow.messaging.rabbitmq.failed");

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
            // IMPORTANT: publishing should not declare queues/bindings.
            // Queues are declared by consumers (SubscribeAsync) so that messages
            // are routed to the correct consumer queue and are not "trapped"
            // into a publisher-owned queue when consumers start later.

            using var activity = MessagingActivitySource.StartActivity($"rabbitmq publish {eventName}", ActivityKind.Producer);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", MainExchange);
            activity?.SetTag("messaging.destination.kind", "exchange");
            activity?.SetTag("messaging.operation", "publish");
            activity?.SetTag("messaging.rabbitmq.routing_key", eventName);
            activity?.SetTag("messaging.message.id", envelope.Metadata.CausationId);
            activity?.SetTag("correlation.id", envelope.Metadata.CorrelationId);

            var json = JsonSerializer.Serialize(envelope);
            var body = Encoding.UTF8.GetBytes(json);

            var props = CreateProperties(envelope.Metadata, activity);

            await _channel.BasicPublishAsync(
                exchange: MainExchange,
                routingKey: eventName,
                mandatory: false,
                basicProperties: props,
                body: body);

            PublishedMessagesCounter.Add(1,
                new KeyValuePair<string, object?>("messaging.destination.name", MainExchange),
                new KeyValuePair<string, object?>("messaging.rabbitmq.routing_key", eventName));
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
                var correlationIdFromHeader = ReadHeaderValue(args.BasicProperties?.Headers, CorrelationIdHeader);
                var causationIdFromHeader = ReadHeaderValue(args.BasicProperties?.Headers, CausationIdHeader);
                var traceParent = ReadHeaderValue(args.BasicProperties?.Headers, TraceParentHeader);
                var baggageHeader = ReadHeaderValue(args.BasicProperties?.Headers, BaggageHeader);

                using var activity = StartConsumerActivity(eventName, queueName, traceParent);
                activity?.SetTag("messaging.message.id", causationIdFromHeader);
                activity?.SetTag("correlation.id", correlationIdFromHeader);

                ApplyBaggage(baggageHeader);

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

                    ConsumedMessagesCounter.Add(1,
                        new KeyValuePair<string, object?>("messaging.source.name", queueName),
                        new KeyValuePair<string, object?>("messaging.rabbitmq.routing_key", eventName));
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    FailedMessagesCounter.Add(1,
                        new KeyValuePair<string, object?>("messaging.source.name", queueName),
                        new KeyValuePair<string, object?>("messaging.rabbitmq.routing_key", eventName));

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
            {
                return;
            }

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

        private static BasicProperties CreateProperties(MessageMetadata metadata, Activity? activity)
        {
            var props = new BasicProperties
            {
                Persistent = true,
                Headers = new Dictionary<string, object?>()
            };

            SetHeader(props.Headers, CorrelationIdHeader, metadata.CorrelationId);
            SetHeader(props.Headers, CausationIdHeader, metadata.CausationId);

            var traceParent = metadata.TraceParent ?? activity?.Id ?? Activity.Current?.Id;
            if (!string.IsNullOrWhiteSpace(traceParent))
            {
                SetHeader(props.Headers, TraceParentHeader, traceParent);
            }

            var baggage = metadata.Baggage ?? BuildBaggageHeaderFromActivity();
            if (!string.IsNullOrWhiteSpace(baggage))
            {
                SetHeader(props.Headers, BaggageHeader, baggage);
            }

            return props;
        }

        private static BasicProperties CreateProperties(IReadOnlyBasicProperties? source)
        {
            var props = source is null ? new BasicProperties() : new BasicProperties(source);
            props.Persistent = true;
            return props;
        }

        private static Activity? StartConsumerActivity(string eventName, string queueName, string? traceParent)
        {
            Activity? activity;

            if (!string.IsNullOrWhiteSpace(traceParent) &&
                ActivityContext.TryParse(traceParent, null, out var parentContext))
            {
                activity = MessagingActivitySource.StartActivity(
                    $"rabbitmq consume {eventName}",
                    ActivityKind.Consumer,
                    parentContext);
            }
            else
            {
                activity = MessagingActivitySource.StartActivity(
                    $"rabbitmq consume {eventName}",
                    ActivityKind.Consumer);
            }

            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.kind", "queue");
            activity?.SetTag("messaging.destination.name", queueName);
            activity?.SetTag("messaging.operation", "process");
            activity?.SetTag("messaging.rabbitmq.routing_key", eventName);

            return activity;
        }

        private static void SetHeader(IDictionary<string, object?> headers, string headerName, string value)
        {
            headers[headerName] = Encoding.UTF8.GetBytes(value);
        }

        private static string? ReadHeaderValue(IDictionary<string, object?>? headers, string headerName)
        {
            if (headers is null || !headers.TryGetValue(headerName, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                ReadOnlyMemory<byte> memoryBytes => Encoding.UTF8.GetString(memoryBytes.Span),
                string stringValue => stringValue,
                _ => value.ToString()
            };
        }

        private static void ApplyBaggage(string? baggageHeader)
        {
            if (string.IsNullOrWhiteSpace(baggageHeader))
            {
                return;
            }

            var entries = baggageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var entry in entries)
            {
                var separatorIndex = entry.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= entry.Length - 1)
                {
                    continue;
                }

                var key = entry[..separatorIndex].Trim();
                var value = entry[(separatorIndex + 1)..].Trim();

                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                Activity.Current?.AddBaggage(key, value);
            }
        }

        private static string? BuildBaggageHeaderFromActivity()
        {
            var activity = Activity.Current;
            if (activity is null)
            {
                return null;
            }

            var entries = new List<string>();
            foreach (var pair in activity.Baggage)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    entries.Add($"{pair.Key}={pair.Value}");
                }
            }

            return entries.Count == 0 ? null : string.Join(",", entries);
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
