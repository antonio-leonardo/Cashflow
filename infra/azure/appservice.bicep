targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short environment tag: dev | staging | prod')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Container registry login server (e.g. cashflowacr.azurecr.io).')
param containerRegistryServer string

@description('Image tag to deploy.')
param imageTag string = 'latest'

@description('Azure Service Bus namespace (FQDN).')
param serviceBusNamespace string

@description('Azure Cache for Redis endpoint.')
param redisEndpoint string

@description('Cosmos DB MongoDB-compatible connection string.')
@secure()
param cosmosDbConnectionString string

@description('Entra ID Tenant ID.')
param entraIdTenantId string

@description('Entra ID Client ID.')
param entraIdClientId string

@description('Entra ID Audience.')
param entraIdAudience string

var prefix = 'cashflow-${environment}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-identity'
  location: location
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${prefix}-asp'
  location: location
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
    size: 'P1v3'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource gatewayApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${prefix}-gateway-web'
  location: location
  kind: 'app,linux,container'
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
      linuxFxVersion: 'DOCKER|${containerRegistryServer}/cashflow-gateway:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: identity.properties.clientId
      appSettings: [
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'Providers__Identity', value: 'EntraId' }
        { name: 'Providers__Secrets', value: 'Local' }
        { name: 'Providers__Messaging', value: 'AzureServiceBus' }
        { name: 'Providers__Telemetry', value: 'ApplicationInsights' }
        { name: 'EntraId__TenantId', value: entraIdTenantId }
        { name: 'EntraId__ClientId', value: entraIdClientId }
        { name: 'EntraId__Audience', value: entraIdAudience }
        { name: 'AzureServiceBus__Namespace', value: serviceBusNamespace }
        { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
      ]
    }
  }
}

resource transactionApi 'Microsoft.Web/sites@2023-12-01' = {
  name: '${prefix}-transaction-api-web'
  location: location
  kind: 'app,linux,container'
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
      linuxFxVersion: 'DOCKER|${containerRegistryServer}/cashflow-transaction-api:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: identity.properties.clientId
      appSettings: [
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'Providers__Identity', value: 'EntraId' }
        { name: 'Providers__Telemetry', value: 'ApplicationInsights' }
        { name: 'EntraId__TenantId', value: entraIdTenantId }
        { name: 'EntraId__ClientId', value: entraIdClientId }
        { name: 'EntraId__Audience', value: entraIdAudience }
      ]
    }
  }
}

resource balanceApi 'Microsoft.Web/sites@2023-12-01' = {
  name: '${prefix}-balance-api-web'
  location: location
  kind: 'app,linux,container'
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
      linuxFxVersion: 'DOCKER|${containerRegistryServer}/cashflow-balance-api:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: identity.properties.clientId
      appSettings: [
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'Providers__Identity', value: 'EntraId' }
        { name: 'Providers__Cache', value: 'AzureRedis' }
        { name: 'Providers__Document', value: 'CosmosDb' }
        { name: 'Providers__Storage', value: 'AzureBlob' }
        { name: 'Providers__Telemetry', value: 'ApplicationInsights' }
        { name: 'EntraId__TenantId', value: entraIdTenantId }
        { name: 'EntraId__ClientId', value: entraIdClientId }
        { name: 'EntraId__Audience', value: entraIdAudience }
        { name: 'AzureRedis__Endpoint', value: redisEndpoint }
        { name: 'AzureRedis__UseManagedIdentity', value: 'true' }
        { name: 'CosmosDb__MongoDB__Connection', value: cosmosDbConnectionString }
      ]
    }
  }
}

output gatewayWebHostName string = gatewayApp.properties.defaultHostName
output transactionApiHostName string = transactionApi.properties.defaultHostName
output balanceApiHostName string = balanceApi.properties.defaultHostName
