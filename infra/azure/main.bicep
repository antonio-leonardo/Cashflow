// ─────────────────────────────────────────────────────────────────────────────
// Cashflow — Azure Container Apps deployment
// Target: one Container Apps Environment shared by Gateway, Balance API,
//         Transaction API, and the three workers (Balance, Audit, Report).
//
// Usage:
//   az deployment group create \
//     --resource-group rg-cashflow \
//     --template-file main.bicep \
//     --parameters @main.parameters.json
// ─────────────────────────────────────────────────────────────────────────────

targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short environment tag: dev | staging | prod')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Container registry login server (e.g. cashflowacr.azurecr.io).')
param containerRegistryServer string

@description('Image tag to deploy (e.g. "20240411.1" or "latest").')
param imageTag string = 'latest'

@description('Azure Service Bus namespace (FQDN, e.g. cashflow-sb.servicebus.windows.net).')
param serviceBusNamespace string

@description('Azure Cache for Redis endpoint (e.g. cashflow-redis.redis.cache.windows.net:6380).')
param redisEndpoint string

@description('Cosmos DB MongoDB-compatible connection string (from Key Vault reference or passed directly).')
@secure()
param cosmosDbConnectionString string

@description('Entra ID Tenant ID for API authentication.')
param entraIdTenantId string

@description('Entra ID Client ID (App Registration) for API authentication.')
param entraIdClientId string

@description('Entra ID Audience (e.g. api://cashflow-api).')
param entraIdAudience string

// ── Shared infrastructure ─────────────────────────────────────────────────────

var prefix = 'cashflow-${environment}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Managed Identity shared by all Container Apps
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-identity'
  location: location
}

// ── Gateway ───────────────────────────────────────────────────────────────────

resource gatewayApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-gateway'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        { server: containerRegistryServer, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'gateway'
          image: '${containerRegistryServer}/cashflow-gateway:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT',     value: environment }
            { name: 'Providers__Identity',         value: 'EntraId' }
            { name: 'Providers__Secrets',          value: 'Local' }
            { name: 'Providers__Messaging',        value: 'AzureServiceBus' }
            { name: 'Providers__Telemetry',        value: 'ApplicationInsights' }
            { name: 'EntraId__TenantId',           value: entraIdTenantId }
            { name: 'EntraId__ClientId',           value: entraIdClientId }
            { name: 'EntraId__Audience',           value: entraIdAudience }
            { name: 'AzureServiceBus__Namespace',  value: serviceBusNamespace }
            { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

// ── Balance Query API ─────────────────────────────────────────────────────────

resource balanceApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-balance-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        { server: containerRegistryServer, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'balance-api'
          image: '${containerRegistryServer}/cashflow-balance-api:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT',      value: environment }
            { name: 'Providers__Identity',          value: 'EntraId' }
            { name: 'Providers__Cache',             value: 'AzureRedis' }
            { name: 'Providers__Telemetry',         value: 'ApplicationInsights' }
            { name: 'EntraId__TenantId',            value: entraIdTenantId }
            { name: 'EntraId__ClientId',            value: entraIdClientId }
            { name: 'EntraId__Audience',            value: entraIdAudience }
            { name: 'AzureRedis__Endpoint',         value: redisEndpoint }
            { name: 'AzureRedis__UseManagedIdentity', value: 'true' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 5 }
    }
  }
}

// ── Worker — Balance ──────────────────────────────────────────────────────────

resource balanceWorker 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-worker-balance'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      registries: [
        { server: containerRegistryServer, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker-balance'
          image: '${containerRegistryServer}/cashflow-worker-balance:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'DOTNET_ENVIRONMENT',           value: environment }
            { name: 'Providers__Messaging',         value: 'AzureServiceBus' }
            { name: 'Providers__Cache',             value: 'AzureRedis' }
            { name: 'Providers__Telemetry',         value: 'ApplicationInsights' }
            { name: 'AzureServiceBus__Namespace',   value: serviceBusNamespace }
            { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
            { name: 'AzureServiceBus__ConsumerName', value: 'balance-worker' }
            { name: 'AzureRedis__Endpoint',         value: redisEndpoint }
            { name: 'AzureRedis__UseManagedIdentity', value: 'true' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

// ── Worker — Audit ────────────────────────────────────────────────────────────

resource auditWorker 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-worker-audit'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      registries: [
        { server: containerRegistryServer, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker-audit'
          image: '${containerRegistryServer}/cashflow-worker-audit:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'DOTNET_ENVIRONMENT',           value: environment }
            { name: 'Providers__Messaging',         value: 'AzureServiceBus' }
            { name: 'Providers__Document',          value: 'CosmosDb' }
            { name: 'Providers__Telemetry',         value: 'ApplicationInsights' }
            { name: 'AzureServiceBus__Namespace',   value: serviceBusNamespace }
            { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
            { name: 'AzureServiceBus__ConsumerName', value: 'audit-worker' }
            { name: 'CosmosDb__MongoDB__Connection', value: cosmosDbConnectionString }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

// ── Worker — Report ───────────────────────────────────────────────────────────

resource reportWorker 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-worker-report'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      registries: [
        { server: containerRegistryServer, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker-report'
          image: '${containerRegistryServer}/cashflow-worker-report:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'DOTNET_ENVIRONMENT',           value: environment }
            { name: 'Providers__Messaging',         value: 'AzureServiceBus' }
            { name: 'Providers__Document',          value: 'CosmosDb' }
            { name: 'Providers__Storage',           value: 'AzureBlob' }
            { name: 'Providers__Telemetry',         value: 'ApplicationInsights' }
            { name: 'AzureServiceBus__Namespace',   value: serviceBusNamespace }
            { name: 'AzureServiceBus__UseManagedIdentity', value: 'true' }
            { name: 'AzureServiceBus__ConsumerName', value: 'report-worker' }
            { name: 'CosmosDb__MongoDB__Connection', value: cosmosDbConnectionString }
            { name: 'AzureBlob__AccountName',       value: '${replace(prefix, \'-\', \'\')}storage' }
            { name: 'AzureBlob__UseManagedIdentity', value: 'true' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 2 }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output gatewayFqdn string = gatewayApp.properties.configuration.ingress.fqdn
output identityClientId string = identity.properties.clientId
