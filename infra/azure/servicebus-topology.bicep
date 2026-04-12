targetScope = 'resourceGroup'

@description('Existing Azure Service Bus namespace name.')
param serviceBusNamespaceName string

@description('Topic used by transaction-created integration events.')
param topicName string = 'transactioncreatedeventv1'

@description('Subscriptions that model worker and smoke-test consumers.')
param subscriptions array = [
  {
    name: 'balance-worker'
    maxDeliveryCount: 10
    requiresSession: false
  }
  {
    name: 'audit-worker'
    maxDeliveryCount: 10
    requiresSession: false
  }
  {
    name: 'report-worker'
    maxDeliveryCount: 10
    requiresSession: false
  }
  {
    name: 'session-test'
    maxDeliveryCount: 3
    requiresSession: true
  }
  {
    name: 'dlq-test'
    maxDeliveryCount: 2
    requiresSession: false
  }
]

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: serviceBusNamespaceName
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  name: '${namespace.name}/${topicName}'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    enableBatchedOperations: true
    supportOrdering: true
  }
}

resource topicSubscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = [
  for subscription in subscriptions: {
    name: '${namespace.name}/${topic.name}/${subscription.name}'
    properties: {
      deadLetteringOnMessageExpiration: true
      defaultMessageTimeToLive: 'P14D'
      lockDuration: 'PT5M'
      maxDeliveryCount: subscription.maxDeliveryCount
      requiresSession: subscription.requiresSession
    }
  }
]

output provisionedTopicName string = topicName
output provisionedSubscriptions array = [for subscription in subscriptions: subscription.name]
