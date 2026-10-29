targetScope = 'resourceGroup'

@description('Globally unique App Service name.')
@minLength(2)
@maxLength(60)
param appServiceName string

@description('Globally unique Azure SQL logical server name.')
@minLength(1)
@maxLength(63)
param sqlServerName string

@description('Name of the private CareerManager database.')
@minLength(1)
@maxLength(128)
param sqlDatabaseName string = 'CareerManager-Production'

@description('Display name of the Microsoft Entra security group that administers this SQL server.')
param sqlAdminGroupName string

@description('Object ID of the Microsoft Entra security group that administers this SQL server.')
param sqlAdminGroupObjectId string

@description('Azure region. Confirm App Service B1 and Azure SQL serverless availability before deployment.')
param location string = resourceGroup().location

var sqlServerFqdn = '${sqlServerName}.${environment().suffixes.sqlServerHostname}'
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlAdmin 'Microsoft.Sql/servers/administrators@2022-05-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: sqlAdminGroupName
    sid: sqlAdminGroupObjectId
    tenantId: subscription().tenantId
  }
}

resource sqlEntraOnly 'Microsoft.Sql/servers/azureADOnlyAuthentications@2021-11-01' = {
  parent: sqlServer
  name: 'Default'
  dependsOn: [
    sqlAdmin
  ]
  properties: {
    azureADOnlyAuthentication: true
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    readScale: 'Disabled'
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appServiceName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
    size: 'B1'
    capacity: 1
  }
  properties: {
    reserved: true
    zoneRedundant: false
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'ApplicationGeneration__Provider'
          value: 'Local'
        }
        {
          name: 'ConnectionStrings__CareerManager'
          value: sqlConnectionString
        }
      ]
    }
  }
  dependsOn: [
    sqlEntraOnly
    sqlDatabase
  ]
}

resource disableScmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-03-01' = {
  parent: appService
  name: 'scm'
  properties: {
    allow: false
  }
}

resource disableFtpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-03-01' = {
  parent: appService
  name: 'ftp'
  properties: {
    allow: false
  }
}

output appServicePrincipalId string = appService.identity.principalId
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServiceOutboundIpAddresses array = split(appService.properties.outboundIpAddresses, ',')
output sqlServerFqdn string = sqlServerFqdn
output sqlDatabaseName string = sqlDatabase.name
