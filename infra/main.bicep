targetScope = 'resourceGroup'

@description('Prefix used to name the lab resources.')
@minLength(3)
@maxLength(18)
param namePrefix string = 'ecomlab'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Microsoft Entra ID tenant ID.')
param tenantId string

@description('Application (client) ID of the API app registration.')
param apiClientId string

@description('Application ID URI exposed by the API, for example api://<client-id>.')
param apiAudience string

@description('Browser origin allowed to call the API through CORS.')
param allowedOrigin string = 'https://localhost:5173'

@description('Log Analytics retention in days.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

var suffix = uniqueString(resourceGroup().id)
var vnetName = '${namePrefix}-vnet'
var appSubnetName = 'snet-app'
var privateEndpointSubnetName = 'snet-pe'
var nsgName = '${namePrefix}-nsg-app'
var logAnalyticsName = '${namePrefix}-law'
var keyVaultName = '${namePrefix}-kv-${suffix}'
var appServicePlanName = '${namePrefix}-plan'
var appName = '${namePrefix}-api-${suffix}'
var appInsightsName = '${namePrefix}-appi'
var keyVaultDnsZoneName = 'privatelink.vaultcore.azure.net'
var appServiceDnsZoneName = 'privatelink.azurewebsites.net'
var authority = '${environment().authentication.loginEndpoint}${tenantId}/v2.0'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionDays
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: nsgName
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowHttpsFromVirtualNetwork'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: 'VirtualNetwork'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '443'
        }
      }
      {
        name: 'AllowAzureLoadBalancer'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'AzureLoadBalancer'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'DenyAllInbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
    ]
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.20.0.0/16'
      ]
    }
    subnets: [
      {
        name: appSubnetName
        properties: {
          addressPrefix: '10.20.1.0/24'
          networkSecurityGroup: {
            id: networkSecurityGroup.id
          }
          delegations: [
            {
              name: 'web'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: '10.20.2.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource appSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: virtualNetwork
  name: appSubnetName
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: virtualNetwork
  name: privateEndpointSubnetName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'None'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Disabled'
    virtualNetworkSubnetId: appSubnet.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      alwaysOn: true
      vnetRouteAllEnabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AzureAd__TenantId'
          value: tenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: apiClientId
        }
        {
          name: 'AzureAd__Audience'
          value: apiAudience
        }
        {
          name: 'AzureAd__Authority'
          value: authority
        }
        {
          name: 'Cors__AllowedOrigins__0'
          value: allowedOrigin
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

// App Service writes console output to the instance filesystem and the platform
// forwards it to AppServiceConsoleLogs. Combined with the diagnostic setting below,
// this is what makes the traceId of a 500 retrievable in Log Analytics.
resource apiAppLogs 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: apiApp
  name: 'logs'
  properties: {
    applicationLogs: {
      fileSystem: {
        level: 'Information'
      }
    }
    httpLogs: {
      fileSystem: {
        enabled: true
        retentionInDays: 7
      }
    }
  }
}

resource apiAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: apiApp
  name: '${appName}-diag'
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: keyVault
  name: '${keyVaultName}-diag'
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
      {
        category: 'AzurePolicyEvaluationDetails'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource keyVaultDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: keyVaultDnsZoneName
  location: 'global'
}

resource appServiceDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: appServiceDnsZoneName
  location: 'global'
}

resource keyVaultDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: keyVaultDnsZone
  name: '${vnetName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource appServiceDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: appServiceDnsZone
  name: '${vnetName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${keyVaultName}-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: '${keyVaultName}-pec'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: keyVaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: keyVaultDnsZone.id
        }
      }
    ]
  }
}

resource appServicePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${appName}-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: '${appName}-pec'
        properties: {
          privateLinkServiceId: apiApp.id
          groupIds: [
            'sites'
          ]
        }
      }
    ]
  }
}

resource appServicePrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: appServicePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sites'
        properties: {
          privateDnsZoneId: appServiceDnsZone.id
        }
      }
    ]
  }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiApp.id, 'key-vault-secrets-user')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    )
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiHostName string = apiApp.properties.defaultHostName
output keyVaultName string = keyVault.name
output logAnalyticsWorkspaceId string = logAnalytics.id
output apiPrincipalId string = apiApp.identity.principalId
