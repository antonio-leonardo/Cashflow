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
        private readonly List<ServiceBusSessionProcessor> _sessionProcessors = new();
        private readonly List<ServiceBusReceiver> _emulatorReceivers = new();
        private readonly List<CancellationTokenSource> _emulatorReceiverCancellationSources = new();
        private readonly List<Task> _emulatorReceiverLoops = new();

        public AzureServiceBusBus(IOptions<AzureServiceBusOptions> options)
        {
            _options = options.Value;
            _client = CreateClient(_options);
        }

        private bool UseEmulatorCompatibilityMode =>
            !string.IsNullOrWhiteSpace(_options.ConnectionString) &&
            _options.ConnectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);

        private static ServiceBusClient CreateClient(AzureServiceBusOptions options)
        {
            var clientOptions = CreateClientOptions(options);

            if (options.UseManagedIdentity && !string.IsNullOrWhiteSpace(options.Namespace))
            {
                return new ServiceBusClient(options.Namespace, new DefaultAzureCredential(), clientOptions);
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString,
                "AzureServiceBus:ConnectionString is required when UseManagedIdentity is false.");

            return new ServiceBusClient(options.ConnectionString, clientOptions);
        }

        private static ServiceBusClientOptions CreateClientOptions(AzureServiceBusOptions options)
        {
            var isEmulator =
                !string.IsNullOrWhiteSpace(options.ConnectionString) &&
                options.ConnectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);

            var clientOptions = new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp,
                RetryOptions =
                {
                    TryTimeout = isEmulator ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(1)
                }
            };

            if (!string.IsNullOrWhiteSpace(options.CustomEndpointAddress))
            {
                clientOptions.CustomEndpointAddress = new Uri(options.CustomEndpointAddress);
            }

            return clientOptions;
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

            if (!string.IsNullOrWhiteSpace(envelope.Metadata.SessionId))
            {
                message.SessionId = envelope.Metadata.SessionId;
            }

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

            var lockRenewal = _options.MaxAutoLockRenewalSeconds > 0
                ? TimeSpan.FromSeconds(_options.MaxAutoLockRenewalSeconds)
                : TimeSpan.Zero;

            if (_options.EnableSessions)
            {
                await SubscribeWithSessionsAsync(topicName, subscriptionName, lockRenewal, handler, cancellationToken);
            }
            else
            {
                await SubscribeWithoutSessionsAsync(topicName, subscriptionName, lockRenewal, handler, cancellationToken);
            }
        }

        private async Task SubscribeWithoutSessionsAsync<TEvent>(
            string topicName,
            string subscriptionName,
            TimeSpan lockRenewal,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            if (UseEmulatorCompatibilityMode)
            {
                await SubscribeWithoutSessionsUsingReceiverAsync(
                    topicName, subscriptionName, handler, cancellationToken);
                return;
            }

            var processor = _client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = _options.MaxConcurrentCalls,
                PrefetchCount = _options.PrefetchCount,
                AutoCompleteMessages = false,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                MaxAutoLockRenewalDuration = lockRenewal
            });

            processor.ProcessMessageAsync += args =>
                HandleMessageAsync(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync,
                    topicName, subscriptionName, handler, args.CancellationToken);

            processor.ProcessErrorAsync += args => HandleProcessorErrorAsync(args, subscriptionName);

            await processor.StartProcessingAsync(cancellationToken);
            _processors.Add(processor);
        }

        private async Task SubscribeWithSessionsAsync<TEvent>(
            string topicName,
            string subscriptionName,
            TimeSpan lockRenewal,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            if (UseEmulatorCompatibilityMode)
            {
                await SubscribeWithSessionsUsingReceiverAsync(
                    topicName, subscriptionName, handler, cancellationToken);
                return;
            }

            var sessionProcessor = _client.CreateSessionProcessor(topicName, subscriptionName,
                new ServiceBusSessionProcessorOptions
                {
                    MaxConcurrentSessions = _options.MaxConcurrentCalls,
                    PrefetchCount = _options.PrefetchCount,
                    AutoCompleteMessages = false,
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    MaxAutoLockRenewalDuration = lockRenewal
                });

            sessionProcessor.ProcessMessageAsync += args =>
                HandleMessageAsync(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync,
                    topicName, subscriptionName, handler, args.CancellationToken);

            sessionProcessor.ProcessErrorAsync += args => HandleProcessorErrorAsync(args, subscriptionName);

            await sessionProcessor.StartProcessingAsync(cancellationToken);
            _sessionProcessors.Add(sessionProcessor);
        }

        private Task SubscribeWithoutSessionsUsingReceiverAsync<TEvent>(
            string topicName,
            string subscriptionName,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            var receiver = _client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
            {
                PrefetchCount = _options.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

            var linkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var loop = Task.Run(() => ReceiveLoopAsync(
                receiver,
                topicName,
                subscriptionName,
                handler,
                linkedCancellationTokenSource.Token), CancellationToken.None);

            _emulatorReceivers.Add(receiver);
            _emulatorReceiverCancellationSources.Add(linkedCancellationTokenSource);
            _emulatorReceiverLoops.Add(loop);

            return Task.CompletedTask;
        }

        private Task SubscribeWithSessionsUsingReceiverAsync<TEvent>(
            string topicName,
            string subscriptionName,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            var linkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var loop = Task.Run(() => SessionReceiveLoopAsync(
                topicName,
                subscriptionName,
                handler,
                linkedCancellationTokenSource.Token), CancellationToken.None);

            _emulatorReceiverCancellationSources.Add(linkedCancellationTokenSource);
            _emulatorReceiverLoops.Add(loop);

            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync<TEvent>(
            ServiceBusReceiver receiver,
            string topicName,
            string subscriptionName,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = await receiver.ReceiveMessageAsync(
                        TimeSpan.FromSeconds(1),
                        cancellationToken);

                    if (message is null)
                    {
                        continue;
                    }

                    await HandleMessageAsync(
                        message,
                        receiver.CompleteMessageAsync,
                        receiver.AbandonMessageAsync,
                        topicName,
                        subscriptionName,
                        handler,
                        cancellationToken);
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task SessionReceiveLoopAsync<TEvent>(
            string topicName,
            string subscriptionName,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ServiceBusSessionReceiver? receiver = null;

                try
                {
                    using var acceptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    acceptTimeout.CancelAfter(TimeSpan.FromSeconds(2));

                    receiver = await _client.AcceptNextSessionAsync(
                        topicName,
                        subscriptionName,
                        new ServiceBusSessionReceiverOptions
                        {
                            PrefetchCount = _options.PrefetchCount,
                            ReceiveMode = ServiceBusReceiveMode.PeekLock
                        },
                        acceptTimeout.Token);
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (TaskCanceledException)
                {
                    continue;
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (ServiceBusException ex) when (
                    ex.Reason is ServiceBusFailureReason.ServiceTimeout or
                    ServiceBusFailureReason.SessionCannotBeLocked)
                {
                    continue;
                }

                await using (receiver.ConfigureAwait(false))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var message = await receiver.ReceiveMessageAsync(
                                TimeSpan.FromSeconds(1),
                                cancellationToken);

                            if (message is null)
                            {
                                break;
                            }

                            await HandleMessageAsync(
                                message,
                                receiver.CompleteMessageAsync,
                                receiver.AbandonMessageAsync,
                                topicName,
                                subscriptionName,
                                handler,
                                cancellationToken);
                        }
                        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }
        }

        private async Task HandleMessageAsync<TEvent>(
            ServiceBusReceivedMessage message,
            Func<ServiceBusReceivedMessage, CancellationToken, Task> completeAsync,
            Func<ServiceBusReceivedMessage, IDictionary<string, object>?, CancellationToken, Task> abandonAsync,
            string topicName,
            string subscriptionName,
            Func<EventEnvelope<TEvent>, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
            where TEvent : IEvent
        {
            var correlationId = message.ApplicationProperties
                .TryGetValue(CorrelationIdProperty, out var corr) ? corr?.ToString() : null;
            var causationId = message.ApplicationProperties
                .TryGetValue(CausationIdProperty, out var caus) ? caus?.ToString() : null;
            var traceParent = message.ApplicationProperties
                .TryGetValue(TraceParentProperty, out var tp) ? tp?.ToString() : null;
            var baggageHeader = message.ApplicationProperties
                .TryGetValue(BaggageProperty, out var bag) ? bag?.ToString() : null;

            using var activity = StartConsumerActivity(typeof(TEvent).Name, topicName, subscriptionName, traceParent);
            activity?.SetTag("messaging.message.id", causationId);
            activity?.SetTag("correlation.id", correlationId);

            ApplyBaggage(baggageHeader);

            try
            {
                var envelope = message.Body.ToObjectFromJson<EventEnvelope<TEvent>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (envelope is null)
                {
                    await completeAsync(message, cancellationToken);
                    return;
                }

                await handler(envelope, cancellationToken);
                await completeAsync(message, cancellationToken);

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

                await abandonAsync(message, null, cancellationToken);
            }
        }

        private Task HandleProcessorErrorAsync(ProcessErrorEventArgs args, string subscriptionName)
        {
            FailedMessagesCounter.Add(1,
                new KeyValuePair<string, object?>("messaging.source.name", subscriptionName),
                new KeyValuePair<string, object?>("error.type", args.Exception.GetType().Name));
            return Task.CompletedTask;
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

        private static async Task DisposeWithTimeoutAsync(IAsyncDisposable disposable, TimeSpan timeout)
        {
            try
            {
                await disposable.DisposeAsync().AsTask().WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                // Avoid hanging test shutdown when the emulator/client SDK blocks on disposal.
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var cancellationTokenSource in _emulatorReceiverCancellationSources)
            {
                cancellationTokenSource.Cancel();
            }

            if (_emulatorReceiverLoops.Count > 0)
            {
                try
                {
                    await Task.WhenAll(_emulatorReceiverLoops).WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TaskCanceledException)
                {
                    // Expected when shutting down background polling loops.
                }
                catch (OperationCanceledException)
                {
                    // Expected when shutting down background polling loops.
                }
                catch (TimeoutException)
                {
                    // Avoid hanging shutdown if the SDK does not promptly cancel outstanding receives.
                }
            }

            foreach (var receiver in _emulatorReceivers)
            {
                await DisposeWithTimeoutAsync(receiver, TimeSpan.FromSeconds(5));
            }

            foreach (var cancellationTokenSource in _emulatorReceiverCancellationSources)
            {
                cancellationTokenSource.Dispose();
            }

            foreach (var processor in _processors)
            {
                await DisposeWithTimeoutAsync(processor, TimeSpan.FromSeconds(5));
            }

            foreach (var sessionProcessor in _sessionProcessors)
            {
                await DisposeWithTimeoutAsync(sessionProcessor, TimeSpan.FromSeconds(5));
            }

            foreach (var sender in _senders.Values)
            {
                await DisposeWithTimeoutAsync(sender, TimeSpan.FromSeconds(5));
            }

            await DisposeWithTimeoutAsync(_client, TimeSpan.FromSeconds(5));
            GC.SuppressFinalize(this);
        }
    }
}
