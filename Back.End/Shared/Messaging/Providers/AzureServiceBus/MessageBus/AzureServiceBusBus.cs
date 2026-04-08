using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Cashflow.Shared.Events;
using Cashflow.Shared.Messaging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace Cashflow.Shared.Messaging.AzureServiceBus.MessageBus
{
    public class AzureServiceBusBus : IMessageBus, IAsyncDisposable
    {
        private const string CorrelationIdProperty = "x-correlation-id";
        private const string CausationIdProperty = "x-causation-id";
        private const string TraceParentProperty = "traceparent";
        private const string BaggageProperty = "baggage";

        private static readonly ActivitySource MessagingActivitySource = new("Cashflow.Messaging");
        private static readonly Meter MessagingMeter = new("Cashflow.Messaging");
        private static readonly Counter<long> PublishedMessagesCounter =
            MessagingMeter.CreateCounter<long>("cashflow.messaging.azureservicebus.published");
        private static readonly Counter<long> ConsumedMessagesCounter =
            MessagingMeter.CreateCounter<long>("cashflow.messaging.azureservicebus.consumed");
        private static readonly Counter<long> FailedMessagesCounter =
            MessagingMeter.CreateCounter<long>("cashflow.messaging.azureservicebus.failed");

        private readonly ServiceBusClient _client;
        private readonly AzureServiceBusOptions _options;
        private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
        private readonly List<ServiceBusProcessor> _processors = new();

        public AzureServiceBusBus(IOptions<AzureServiceBusOptions> options)
        {
            _options = options.Value;
            _client = CreateClient(_options);
        }

        private static ServiceBusClient CreateClient(AzureServiceBusOptions options)
        {
            if (options.UseManagedIdentity && !string.IsNullOrWhiteSpace(options.Namespace))
            {
                return new ServiceBusClient(options.Namespace, new DefaultAzureCredential());
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString,
                "AzureServiceBus:ConnectionString is required when UseManagedIdentity is false.");

            return new ServiceBusClient(options.ConnectionString);
        }

        public async Task PublishAsync<TEvent>(
            EventEnvelope<TEvent> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var topicName = typeof(TEvent).Name.ToLowerInvariant();

            using var activity = MessagingActivitySource.StartActivity(
                $"servicebus publish {typeof(TEvent).Name}", ActivityKind.Producer);
            activity?.SetTag("messaging.system", "servicebus");
            activity?.SetTag("messaging.destination.name", topicName);
            activity?.SetTag("messaging.destination.kind", "topic");
            activity?.SetTag("messaging.operation", "publish");
            activity?.SetTag("messaging.message.id", envelope.Metadata.CausationId);
            activity?.SetTag("correlation.id", envelope.Metadata.CorrelationId);

            var sender = _senders.GetOrAdd(topicName, name => _client.CreateSender(name));

            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(envelope))
            {
                ContentType = "application/json",
                CorrelationId = envelope.Metadata.CorrelationId,
                MessageId = envelope.Metadata.CausationId,
                Subject = typeof(TEvent).Name
            };

            message.ApplicationProperties[CorrelationIdProperty] = envelope.Metadata.CorrelationId;
            message.ApplicationProperties[CausationIdProperty] = envelope.Metadata.CausationId;

            var traceParent = envelope.Metadata.TraceParent ?? activity?.Id ?? Activity.Current?.Id;
            if (!string.IsNullOrWhiteSpace(traceParent))
            {
                message.ApplicationProperties[TraceParentProperty] = traceParent;
            }

            var baggage = envelope.Metadata.Baggage ?? BuildBaggageHeaderFromActivity();
            if (!string.IsNullOrWhiteSpace(baggage))
            {
                message.ApplicationProperties[BaggageProperty] = baggage;
            }

            await sender.SendMessageAsync(message, cancellationToken);

            PublishedMessagesCounter.Add(1,
                new KeyValuePair<string, object?>("messaging.destination.name", topicName));
        }

        public async Task SubscribeAsync<TEvent>(
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var topicName = typeof(TEvent).Name.ToLowerInvariant();
            var subscriptionName = string.IsNullOrWhiteSpace(_options.ConsumerName)
                ? typeof(TEvent).Name
                : _options.ConsumerName;

            var processor = _client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = _options.MaxConcurrentCalls,
                AutoCompleteMessages = false,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

            processor.ProcessMessageAsync += async args =>
            {
                var correlationId = args.Message.ApplicationProperties
                    .TryGetValue(CorrelationIdProperty, out var corr) ? corr?.ToString() : null;
                var causationId = args.Message.ApplicationProperties
                    .TryGetValue(CausationIdProperty, out var caus) ? caus?.ToString() : null;
                var traceParent = args.Message.ApplicationProperties
                    .TryGetValue(TraceParentProperty, out var tp) ? tp?.ToString() : null;
                var baggageHeader = args.Message.ApplicationProperties
                    .TryGetValue(BaggageProperty, out var bag) ? bag?.ToString() : null;

                using var activity = StartConsumerActivity(typeof(TEvent).Name, topicName, subscriptionName, traceParent);
                activity?.SetTag("messaging.message.id", causationId);
                activity?.SetTag("correlation.id", correlationId);

                ApplyBaggage(baggageHeader);

                try
                {
                    var envelope = args.Message.Body.ToObjectFromJson<EventEnvelope<TEvent>>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (envelope is null)
                    {
                        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                        return;
                    }

                    await handler(envelope, args.CancellationToken);
                    await args.CompleteMessageAsync(args.Message, args.CancellationToken);

                    ConsumedMessagesCounter.Add(1,
                        new KeyValuePair<string, object?>("messaging.source.name", subscriptionName),
                        new KeyValuePair<string, object?>("messaging.destination.name", topicName));
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    FailedMessagesCounter.Add(1,
                        new KeyValuePair<string, object?>("messaging.source.name", subscriptionName),
                        new KeyValuePair<string, object?>("messaging.destination.name", topicName));

                    await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
                }
            };

            processor.ProcessErrorAsync += args =>
            {
                FailedMessagesCounter.Add(1,
                    new KeyValuePair<string, object?>("messaging.source.name", subscriptionName),
                    new KeyValuePair<string, object?>("error.type", args.Exception.GetType().Name));
                return Task.CompletedTask;
            };

            await processor.StartProcessingAsync(cancellationToken);
            _processors.Add(processor);
        }

        private static Activity? StartConsumerActivity(
            string eventName, string topicName, string subscriptionName, string? traceParent)
        {
            Activity? activity;

            if (!string.IsNullOrWhiteSpace(traceParent) &&
                ActivityContext.TryParse(traceParent, null, out var parentContext))
            {
                activity = MessagingActivitySource.StartActivity(
                    $"servicebus consume {eventName}",
                    ActivityKind.Consumer,
                    parentContext);
            }
            else
            {
                activity = MessagingActivitySource.StartActivity(
                    $"servicebus consume {eventName}",
                    ActivityKind.Consumer);
            }

            activity?.SetTag("messaging.system", "servicebus");
            activity?.SetTag("messaging.destination.kind", "topic");
            activity?.SetTag("messaging.destination.name", topicName);
            activity?.SetTag("messaging.servicebus.subscription.name", subscriptionName);
            activity?.SetTag("messaging.operation", "process");

            return activity;
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

                if (key.Length > 0 && value.Length > 0)
                {
                    Activity.Current?.AddBaggage(key, value);
                }
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

        public async ValueTask DisposeAsync()
        {
            foreach (var processor in _processors)
            {
                await processor.DisposeAsync();
            }

            foreach (var sender in _senders.Values)
            {
                await sender.DisposeAsync();
            }

            await _client.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
