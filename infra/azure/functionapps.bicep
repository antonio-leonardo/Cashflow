targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short environment tag: dev | staging | prod')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Azure Service Bus namespace (FQDN).')
param serviceBusNamespace string

@description('Azure Cache for Redis endpoint.')
param redisEndpoint string

@description('Cosmos DB MongoDB-compatible connection string.')
@secure()
param cosmosDbConnectionString string

@description('Blob storage account name used by the report function.')
param blobStorageAccountName string

var prefix = 'cashflow-${environment}'
var storageAccountName = toLower(replace('${prefix}funcsa', '-', ''))

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-functions-identity'
  location: location
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${prefix}-functions-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
}

resource balanceFunction 'Microsoft.Web/sites@2023-12-01' = {
  name: '${prefix}-balance-func'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      appSettings: [
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'AzureServiceBus__Namespace', value: serviceBusNamespace }
        { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
        { name: 'AzureServiceBus__ConsumerName', value: 'balance-worker' }
        { name: 'AzureServiceBus__TopicName', value: 'transactioncreatedeventv1' }
        { name: 'Providers__Cache', value: 'AzureRedis' }
        { name: 'Providers__Telemetry', value: 'ApplicationInsights' }
        { name: 'AzureRedis__Endpoint', value: redisEndpoint }
        { name: 'AzureRedis__UseManagedIdentity', value: 'true' }
      ]
    }
  }
}

resource reportFunction 'Microsoft.Web/sites@2023-12-01' = {
  name: '${prefix}-report-func'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      appSettings: [
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'AzureServiceBus__Namespace', value: serviceBusNamespace }
        { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
        { name: 'AzureServiceBus__ConsumerName', value: 'report-worker' }
        { name: 'AzureServiceBus__TopicName', value: 'transactioncreatedeventv1' }
        { name: 'Providers__Document', value: 'CosmosDb' }
        { name: 'Providers__Storage', value: 'AzureBlob' }
        { name: 'Providers__Telemetry', value: 'ApplicationInsights' }
        { name: 'CosmosDb__MongoDB__Connection', value: cosmosDbConnectionString }
        { name: 'AzureBlob__AccountName', value: blobStorageAccountName }
        { name: 'AzureBlob__UseManagedIdentity', value: 'true' }
      ]
    }
  }
}

output balanceFunctionName string = balanceFunction.name
output reportFunctionName string = reportFunction.name
