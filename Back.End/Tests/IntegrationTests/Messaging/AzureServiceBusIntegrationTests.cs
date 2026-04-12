using Azure.Messaging.ServiceBus;
using Cashflow.Service.Transaction.Domain;
using Cashflow.Shared.Messaging.Abstractions;
using Cashflow.Shared.Messaging.AzureServiceBus.MessageBus;
using Infrastructure.Test;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Messaging.Integration.Tests
{
    /// <summary>
    /// Integration tests for <see cref="AzureServiceBusBus"/> against the official
    /// Microsoft Service Bus Emulator (Docker).
    ///
    /// These tests verify that the Azure Service Bus provider delivers the same
    /// behavioural guarantees as the RabbitMQ provider:
    ///   - Fan-out delivery to independent subscriptions
    ///   - Correlation / baggage header propagation
    ///   - Message abandonment and DLQ routing when MaxDeliveryCount is exhausted
    ///   - Session-ordered processing when EnableSessions=true
    ///
    /// Requires Docker with at least 2 GB RAM for SQL Server + Emulator containers.
    /// Tests are categorised with [Trait("Category","ServiceBusEmulator")] so they
    /// can be excluded on resource-constrained CI agents:
    ///   dotnet test --filter "Category!=ServiceBusEmulator"
    /// </summary>
    [Collection("ServiceBusEmulatorCollection")]
    [Trait("Category", "ServiceBusEmulator")]
    public class AzureServiceBusIntegrationTests
    {
        private readonly ServiceBusEmulatorFixture _fixture;
        private readonly ITestOutputHelper _output;

        public AzureServiceBusIntegrationTests(ServiceBusEmulatorFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private AzureServiceBusBus CreateBus(string consumerName, bool enableSessions = false)
        {
            var options = Options.Create(new AzureServiceBusOptions
            {
                ConnectionString         = _fixture.ConnectionString,
                CustomEndpointAddress    = _fixture.CustomEndpointAddress?.ToString(),
                ConsumerName             = consumerName,
                MaxConcurrentCalls       = 1,
                MaxAutoLockRenewalSeconds = 60,
                EnableSessions           = enableSessions
            });
            return new AzureServiceBusBus(options);
        }

        private ServiceBusClient CreateClient()
        {
            var options = new ServiceBusClientOptions();
            if (_fixture.CustomEndpointAddress is not null)
            {
                options.CustomEndpointAddress = _fixture.CustomEndpointAddress;
            }

            return new ServiceBusClient(_fixture.ConnectionString, options);
        }

        private async Task DrainSubscriptionAsync(
            string subscriptionName,
            bool enableSessions = false,
            bool readDeadLetterQueue = false,
            CancellationToken cancellationToken = default)
        {
            if (enableSessions)
            {
                await DrainSessionSubscriptionAsync(subscriptionName, cancellationToken);
                return;
            }

            await using var client = CreateClient();
            await using var receiver = client.CreateReceiver(
                "transactioncreatedeventv1",
                subscriptionName,
                new ServiceBusReceiverOptions
                {
                    SubQueue = readDeadLetterQueue
                        ? Azure.Messaging.ServiceBus.SubQueue.DeadLetter
                        : Azure.Messaging.ServiceBus.SubQueue.None
                });

            while (true)
            {
                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: 20,
                    maxWaitTime: TimeSpan.FromMilliseconds(500),
                    cancellationToken);

                if (messages.Count == 0)
                {
                    return;
                }

                foreach (var message in messages)
                {
                    await receiver.CompleteMessageAsync(message, cancellationToken);
                }
            }
        }

        private async Task DrainSessionSubscriptionAsync(
            string subscriptionName,
            CancellationToken cancellationToken)
        {
            await using var client = CreateClient();

            while (!cancellationToken.IsCancellationRequested)
            {
                ServiceBusSessionReceiver? receiver = null;

                try
                {
                    using var acceptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    acceptTimeout.CancelAfter(TimeSpan.FromMilliseconds(500));

                    receiver = await client.AcceptNextSessionAsync(
                        "transactioncreatedeventv1",
                        subscriptionName,
                        new ServiceBusSessionReceiverOptions(),
                        acceptTimeout.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ServiceBusException ex) when (
                    ex.Reason is ServiceBusFailureReason.ServiceTimeout or
                    ServiceBusFailureReason.SessionCannotBeLocked)
                {
                    return;
                }

                await using (receiver.ConfigureAwait(false))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var messages = await receiver.ReceiveMessagesAsync(
                            maxMessages: 20,
                            maxWaitTime: TimeSpan.FromMilliseconds(250),
                            cancellationToken);

                        if (messages.Count == 0)
                        {
                            break;
                        }

                        foreach (var message in messages)
                        {
                            await receiver.CompleteMessageAsync(message, cancellationToken);
                        }
                    }
                }
            }
        }

        private static TransactionCreatedEventV1 BuildEvent(decimal amount = 150m) =>
            new(Guid.NewGuid(), Guid.NewGuid(), amount, "BRL", TransactionType.Credit);

        private static EventEnvelope<TransactionCreatedEventV1> BuildEnvelope(
            TransactionCreatedEventV1 evt,
            string? correlationId = null,
            string? sessionId = null) =>
            new(evt, new MessageMetadata(
                CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
                CausationId:   evt.EventId.ToString(),
                Source:        "AzureServiceBusIntegrationTests",
                TenantId:      null,
                CreatedAtUtc:  DateTime.UtcNow,
                SessionId:     sessionId));

        private async Task AwaitStepAsync(Task task, string step, TimeSpan timeout)
        {
            _output.WriteLine($"Starting step: {step}");
            await task.WaitAsync(timeout);
            _output.WriteLine($"Completed step: {step}");
        }

        private async Task<T> AwaitStepAsync<T>(Task<T> task, string step, TimeSpan timeout)
        {
            _output.WriteLine($"Starting step: {step}");
            var result = await task.WaitAsync(timeout);
            _output.WriteLine($"Completed step: {step}");
            return result;
        }

        // ── fanout ───────────────────────────────────────────────────────────

        [Fact]
        public async Task Should_Deliver_Same_Message_To_Independent_Subscriptions()
        {
            // consumer-a and consumer-b are provisioned in ServiceBusEmulatorFixture Config.json
            await using var consumerA  = CreateBus("consumer-a");
            await using var consumerB  = CreateBus("consumer-b");
            await using var publisher  = CreateBus(string.Empty);

            var receivedA = new TaskCompletionSource<EventEnvelope<TransactionCreatedEventV1>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var receivedB = new TaskCompletionSource<EventEnvelope<TransactionCreatedEventV1>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await DrainSubscriptionAsync("consumer-a", cancellationToken: cts.Token);
            await DrainSubscriptionAsync("consumer-b", cancellationToken: cts.Token);

            await AwaitStepAsync(
                consumerA.SubscribeAsync<TransactionCreatedEventV1>(
                    (e, _) => { receivedA.TrySetResult(e); return Task.CompletedTask; }, cts.Token),
                "subscribe consumer-a",
                TimeSpan.FromSeconds(10));
            await AwaitStepAsync(
                consumerB.SubscribeAsync<TransactionCreatedEventV1>(
                    (e, _) => { receivedB.TrySetResult(e); return Task.CompletedTask; }, cts.Token),
                "subscribe consumer-b",
                TimeSpan.FromSeconds(10));

            await Task.Delay(500, cts.Token);

            var evt      = BuildEvent();
            var corrId   = Guid.NewGuid().ToString();
            var envelope = BuildEnvelope(evt, corrId);

            await AwaitStepAsync(
                publisher.PublishAsync(envelope, cts.Token),
                "publish fan-out event",
                TimeSpan.FromSeconds(10));

            var msgA = await AwaitStepAsync(receivedA.Task, "receive consumer-a", TimeSpan.FromSeconds(10));
            var msgB = await AwaitStepAsync(receivedB.Task, "receive consumer-b", TimeSpan.FromSeconds(10));

            Assert.Equal(corrId, msgA.Metadata.CorrelationId);
            Assert.Equal(corrId, msgB.Metadata.CorrelationId);
            Assert.Equal(evt.AccountId, msgA.Event.AccountId);
            Assert.Equal(evt.AccountId, msgB.Event.AccountId);
        }

        // ── correlation propagation ───────────────────────────────────────────

        [Fact]
        public async Task Should_Propagate_CorrelationId_And_CausationId()
        {
            await using var consumer  = CreateBus("consumer-a");
            await using var publisher = CreateBus(string.Empty);

            var received = new TaskCompletionSource<EventEnvelope<TransactionCreatedEventV1>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await DrainSubscriptionAsync("consumer-a", cancellationToken: cts.Token);

            await AwaitStepAsync(
                consumer.SubscribeAsync<TransactionCreatedEventV1>(
                    (e, _) => { received.TrySetResult(e); return Task.CompletedTask; }, cts.Token),
                "subscribe correlation consumer",
                TimeSpan.FromSeconds(10));

            await Task.Delay(300, cts.Token);

            var evt      = BuildEvent();
            var corrId   = Guid.NewGuid().ToString();
            var envelope = BuildEnvelope(evt, corrId);

            await AwaitStepAsync(
                publisher.PublishAsync(envelope, cts.Token),
                "publish correlation event",
                TimeSpan.FromSeconds(10));

            var msg = await AwaitStepAsync(received.Task, "receive correlation event", TimeSpan.FromSeconds(10));

            Assert.Equal(corrId,                msg.Metadata.CorrelationId);
            Assert.Equal(evt.EventId.ToString(), msg.Metadata.CausationId);
        }

        // ── DLQ ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task Should_Abandon_Message_And_Reach_DLQ_After_MaxDeliveryCount()
        {
            // "dlq-test" subscription has MaxDeliveryCount=2 in Config.json
            await using var consumer  = CreateBus("dlq-test");
            await using var publisher = CreateBus(string.Empty);

            var processingAttempts = 0;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await DrainSubscriptionAsync("dlq-test", cancellationToken: cts.Token);
            await DrainSubscriptionAsync("dlq-test", readDeadLetterQueue: true, cancellationToken: cts.Token);

            await AwaitStepAsync(
                consumer.SubscribeAsync<TransactionCreatedEventV1>((_, _) =>
                {
                    Interlocked.Increment(ref processingAttempts);
                    throw new InvalidOperationException("Forced failure for DLQ test");
                }, cts.Token),
                "subscribe dlq consumer",
                TimeSpan.FromSeconds(10));

            await Task.Delay(300, cts.Token);

            var evt = BuildEvent();
            await AwaitStepAsync(
                publisher.PublishAsync(BuildEnvelope(evt), cts.Token),
                "publish dlq event",
                TimeSpan.FromSeconds(10));

            // Wait long enough for MaxDeliveryCount (2) retries to exhaust and the message to reach DLQ
            await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);

            await using var dlqClient = CreateClient();
            var dlqReceiver = dlqClient.CreateReceiver(
                "transactioncreatedeventv1",
                "dlq-test",
                new ServiceBusReceiverOptions
                {
                    SubQueue = Azure.Messaging.ServiceBus.SubQueue.DeadLetter
                });

            var dlqMessage = await dlqReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10), cts.Token);

            // Verify at least MaxDeliveryCount attempts were made before DLQ routing
            Assert.True(processingAttempts >= 2,
                $"Expected at least 2 delivery attempts, got {processingAttempts}.");
            Assert.NotNull(dlqMessage);
            Assert.Equal(typeof(TransactionCreatedEventV1).Name, dlqMessage.Subject);
        }

        [Fact]
        public async Task Should_Process_Session_Enabled_Subscription_In_Publish_Order_For_Same_Session()
        {
            await using var consumer = CreateBus("session-test", enableSessions: true);
            await using var publisher = CreateBus(string.Empty);

            var processedAmounts = new List<decimal>();
            var completion = new TaskCompletionSource<IReadOnlyList<decimal>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var sessionCorrelationId = Guid.NewGuid().ToString();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await DrainSubscriptionAsync("session-test", enableSessions: true, cancellationToken: cts.Token);

            await AwaitStepAsync(
                consumer.SubscribeAsync<TransactionCreatedEventV1>((envelope, _) =>
                {
                    lock (processedAmounts)
                    {
                        processedAmounts.Add(envelope.Event.Amount);
                        if (processedAmounts.Count == 2)
                        {
                            completion.TrySetResult(processedAmounts.ToArray());
                        }
                    }

                    return Task.CompletedTask;
                }, cts.Token),
                "subscribe session consumer",
                TimeSpan.FromSeconds(10));

            await Task.Delay(500, cts.Token);

            var first = BuildEnvelope(BuildEvent(10m), sessionCorrelationId, sessionCorrelationId);
            var second = BuildEnvelope(BuildEvent(20m), sessionCorrelationId, sessionCorrelationId);

            await AwaitStepAsync(
                publisher.PublishAsync(first, cts.Token),
                "publish first session event",
                TimeSpan.FromSeconds(10));
            await AwaitStepAsync(
                publisher.PublishAsync(second, cts.Token),
                "publish second session event",
                TimeSpan.FromSeconds(10));

            var orderedAmounts = await AwaitStepAsync(completion.Task, "receive ordered session events", TimeSpan.FromSeconds(10));
            Assert.Equal(new[] { 10m, 20m }, orderedAmounts);
        }

    }
}
