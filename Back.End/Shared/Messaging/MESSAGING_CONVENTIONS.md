# Messaging Conventions

This document covers naming, topology, and configuration conventions for the Cashflow messaging layer.
Both providers — **RabbitMQ** and **Azure Service Bus** — follow the same rules so that switching
`Providers:Messaging` in `appsettings.json` requires no code changes.

---

## Topic / Queue naming

| Rule | Pattern | Example |
|------|---------|---------|
| Topic name | `typeof(TEvent).Name.ToLowerInvariant()` | `transactioncreatedeventv1` |
| Subscription / consumer group | `AzureServiceBus:ConsumerName` / `RabbitMq:ConsumerName` | `balance-worker` |
| DLQ (RabbitMQ) | `{consumerName}.{EventTypeName}.dlq` | `balance-worker.TransactionCreatedEventV1.dlq` |
| DLQ (Azure Service Bus) | Managed automatically by the broker when `MaxDeliveryCount` is reached on the entity | `$DeadLetterQueue` sub-queue |

### Topic name derivation

```csharp
var topicName = typeof(TEvent).Name.ToLowerInvariant();
// TransactionCreatedEventV1 → "transactioncreatedeventv1"
```

All event types must have **globally unique class names** because the topic name is derived from the
CLR type name. If two assemblies define a class with the same name, messages will be routed to the
same topic unexpectedly.

---

## Subscription / consumer group naming

Configure one consumer name per worker / service:

```json
// appsettings.json — RabbitMQ
"RabbitMq": { "ConsumerName": "balance-worker" }

// appsettings.json — Azure Service Bus
"AzureServiceBus": { "ConsumerName": "balance-worker" }
```

Each consumer name must be **unique per application instance type**. Two Balance Worker replicas
should share the same name so they compete for the same messages (competing consumers pattern).

---

## Azure Service Bus entity topology

When `Providers:Messaging = AzureServiceBus`, the expected topology is:

```
Topic: transactioncreatedeventv1
  ├── Subscription: balance-worker     (MaxDeliveryCount=10 recommended)
  ├── Subscription: audit-worker       (MaxDeliveryCount=10 recommended)
  └── Subscription: report-worker      (MaxDeliveryCount=10 recommended)
```

> **Important**: `MaxDeliveryCount` is a **server-side** property of the subscription entity.
> It cannot be configured from the client SDK (`AzureServiceBusOptions`).
> Configure it via the Azure portal, bicep/ARM, or the Service Bus management SDK.
> The client will automatically abandon messages that fail; the broker moves them to the
> `$DeadLetterQueue` sub-queue once `MaxDeliveryCount` is exhausted.

Recommended entity settings:

| Property | Value | Reason |
|----------|-------|--------|
| `MaxDeliveryCount` | 10 | Allows transient failures before DLQ |
| `LockDuration` | PT2M | Matches default `MaxAutoLockRenewalSeconds=300` in options |
| `DefaultMessageTimeToLive` | P14D | 14 days — align with business SLA |
| `EnableDeadLettering` | true | Automatic |

---

## Client-side options reference

```json
"AzureServiceBus": {
  "ConnectionString": "...",            // use OR Namespace+UseManagedIdentity
  "Namespace":        "...",
  "UseManagedIdentity": true,
  "ConsumerName":     "balance-worker",
  "MaxConcurrentCalls":   1,            // parallelism per subscription
  "PrefetchCount":        0,            // 0 = disabled; increase for high-throughput
  "MaxAutoLockRenewalSeconds": 300,     // client-side lock renewal (0 = disabled)
  "EnableSessions":   false             // set true only for session-enabled entities
}
```

### `EnableSessions`

When `true`, `AzureServiceBusBus` uses `ServiceBusSessionProcessor`. The subscription **must** have
`RequiresSession = true` configured at creation time. Session-enabled entities guarantee ordered
delivery per session ID. Use this only for workflows that require ordering (e.g., saga orchestration).

---

## RabbitMQ topology

```
Exchange: transactioncreatedeventv1   (fanout, durable)
  ├── Queue: balance-worker.TransactionCreatedEventV1           (durable)
  │     └── DLX → balance-worker.TransactionCreatedEventV1.dlq
  ├── Queue: audit-worker.TransactionCreatedEventV1             (durable)
  └── Queue: report-worker.TransactionCreatedEventV1            (durable)
```

Retry is handled via a dead-letter exchange with `x-message-ttl` backoff.
`RabbitMqOptions.RetryCount` and `RetryDelaySeconds` control the client-side retry loop before
the message is sent to the DLQ.

---

## Correlation and tracing headers

Both providers propagate the following headers on every message:

| Header | Description |
|--------|-------------|
| `x-correlation-id` | Business correlation ID (from `MessageMetadata.CorrelationId`) |
| `x-causation-id` | ID of the command/event that caused this message |
| `traceparent` | W3C Trace Context parent span ID |
| `baggage` | W3C Baggage header (comma-separated `key=value` pairs) |
