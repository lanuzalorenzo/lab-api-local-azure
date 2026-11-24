targetScope = 'resourceGroup'

@description('Workload name used by every resource name: lower-case letters and digits, without separators; the Key Vault limit sets the seven-character cap.')
@minLength(3)
@maxLength(7)
param workload string = 'ecomlab'

@description('Environment segment of every resource name.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'dev'

@description('Azure region for the deployment.')
@allowed([
  'westeurope'
  'northeurope'
  'spaincentral'
  'eastus'
])
param location string = 'westeurope'

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

// Naming: one function receives the CAF type abbreviation, the workload, the
// environment and the region, so no resource name is written by hand. The CI
// check that rejects hand-written names is still pending.
func resourceName(resourceType string, workloadName string, environmentName string, regionShort string, instanceSuffix string) string =>
  '${resourceType}-${workloadName}-${environmentName}-${regionShort}-${instanceSuffix}'

// CAF short code per region; this table is the only place the region becomes a name.
var regionCodes = {
  westeurope: 'weu'
  northeurope: 'neu'
  spaincentral: 'esc'
  eastus: 'eus'
}

// Bicep has no regex decorator, so the deploy stops before creating anything when
// the workload carries uppercase, a separator or a space. Any other invalid
// character is rejected by Azure with the resource name in the message.
var workloadIsValid = workload == toLower(workload) && !contains(workload, '-') && !contains(workload, '_') && !contains(workload, ' ')
var workloadName = workloadIsValid ? workload : fail('workload must use lower-case letters and digits, without separators.')
var instanceSuffix = substring(uniqueString(resourceGroup().id), 0, 4)

// Four characters keep the Key Vault name inside its 24-character limit for every
// allowed region and environment.
var regionCode = regionCodes[?location] ?? fail('location is not in regionCodes.')

var names = {
  vnet: resourceName('vnet', workloadName, environmentName, regionCode, instanceSuffix)
  appSubnet: resourceName('snet', workloadName, environmentName, regionCode, 'app')
  privateEndpointSubnet: resourceName('snet', workloadName, environmentName, regionCode, 'pe')
  nsg: resourceName('nsg', workloadName, environmentName, regionCode, instanceSuffix)
  logAnalytics: resourceName('log', workloadName, environmentName, regionCode, instanceSuffix)
  keyVault: resourceName('kv', workloadName, environmentName, regionCode, instanceSuffix)
  appServicePlan: resourceName('plan', workloadName, environmentName, regionCode, instanceSuffix)
  apiApp: resourceName('app', workloadName, environmentName, regionCode, instanceSuffix)
  appInsights: resourceName('appi', workloadName, environmentName, regionCode, instanceSuffix)
  apiDiagnostics: resourceName('diag', workloadName, environmentName, regionCode, 'app')
  keyVaultDiagnostics: resourceName('diag', workloadName, environmentName, regionCode, 'kv')
  keyVaultPrivateEndpoint: resourceName('pe', workloadName, environmentName, regionCode, 'kv')
  appServicePrivateEndpoint: resourceName('pe', workloadName, environmentName, regionCode, 'app')
  keyVaultConnection: resourceName('pec', workloadName, environmentName, regionCode, 'kv')
  appServiceConnection: resourceName('pec', workloadName, environmentName, regionCode, 'app')
  keyVaultDnsGroup: resourceName('pdnszg', workloadName, environmentName, regionCode, 'kv')
  appServiceDnsGroup: resourceName('pdnszg', workloadName, environmentName, regionCode, 'app')
  keyVaultVnetLink: resourceName('vnetlink', workloadName, environmentName, regionCode, 'kv')
  appServiceVnetLink: resourceName('vnetlink', workloadName, environmentName, regionCode, 'app')
}

// Private DNS zone names are fixed by Azure, not chosen.
var keyVaultDnsZoneName = 'privatelink.vaultcore.azure.net'
var appServiceDnsZoneName = 'privatelink.azurewebsites.net'
var authority = '${environment().authentication.loginEndpoint}${tenantId}/v2.0'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: names.logAnalytics
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionDays
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: names.appInsights
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: names.nsg
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
  name: names.vnet
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.20.0.0/16'
      ]
    }
    subnets: [
      {
        name: names.appSubnet
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
        name: names.privateEndpointSubnet
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
  name: names.appSubnet
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: virtualNetwork
  name: names.privateEndpointSubnet
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: names.keyVault
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
  name: names.appServicePlan
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
  name: names.apiApp
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

// Linux App Service keeps a file-system copy of application logs for Kudu and
// streaming. Console and HTTP logs reach Log Analytics through the platform
// diagnostic categories below; the Windows-only httpLogs block is not declared.
// The child name is fixed by the platform.
resource apiAppLogs 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: apiApp
  name: 'logs'
  properties: {
    applicationLogs: {
      fileSystem: {
        level: 'Information'
      }
    }
  }
}

resource apiAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: apiApp
  name: names.apiDiagnostics
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
  name: names.keyVaultDiagnostics
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
  name: names.keyVaultVnetLink
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
  name: names.appServiceVnetLink
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: names.keyVaultPrivateEndpoint
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: names.keyVaultConnection
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
  name: names.keyVaultDnsGroup
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
  name: names.appServicePrivateEndpoint
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: names.appServiceConnection
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
  name: names.appServiceDnsGroup
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
