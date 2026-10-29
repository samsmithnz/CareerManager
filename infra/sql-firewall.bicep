targetScope = 'resourceGroup'

@description('Name of the Azure SQL logical server created by infra/main.bicep.')
param sqlServerName string

@description('Current App Service outbound IPv4 addresses from the main deployment output.')
@minLength(1)
param appServiceOutboundIpAddresses array

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: sqlServerName
}

resource appServiceFirewallRules 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = [for (ip, index) in appServiceOutboundIpAddresses: {
  parent: sqlServer
  name: 'app-service-${index}'
  properties: {
    startIpAddress: ip
    endIpAddress: ip
  }
}]
