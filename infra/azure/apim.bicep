targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short environment tag: dev | staging | prod')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('API Management publisher name.')
param publisherName string

@description('API Management publisher email.')
param publisherEmail string

@description('Gateway hostname already deployed in Azure.')
param gatewayHostname string

var prefix = 'cashflow-${environment}'

resource apim 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: '${prefix}-apim'
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherName: publisherName
    publisherEmail: publisherEmail
  }
}

resource backend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = {
  name: '${apim.name}/cashflow-gateway-backend'
  properties: {
    protocol: 'http'
    url: 'https://${gatewayHostname}'
    description: 'Cashflow Gateway backend'
  }
}

resource api 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  name: '${apim.name}/cashflow-gateway'
  properties: {
    displayName: 'Cashflow Gateway'
    path: 'cashflow'
    protocols: [
      'https'
    ]
    serviceUrl: 'https://${gatewayHostname}'
    subscriptionRequired: false
  }
}

output apiManagementGatewayUrl string = apim.properties.gatewayUrl
output publishedApiPath string = api.properties.path
