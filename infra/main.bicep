// Ironbell M0 infrastructure.
//
// Everything here is sized for the free tiers described in ADR 0001 / plan v3: Azure SQL's free
// offer (permanent, not a 12-month trial) and Container Apps consumption scaled to zero. The
// expected steady-state cost through M0-M6 is the domain registration and little else.
//
// Deployed by hand, not by CI. Provisioning is rare, occasionally destructive, and worth watching;
// the pipeline only ever updates the container image and applies migrations.

targetScope = 'resourceGroup'

@description('Azure region for every resource.')
param location string = 'northeurope'

@description('Prefix for resource names. Must be globally unique where Azure demands it.')
@minLength(3)
@maxLength(12)
param namePrefix string = 'ironbell'

@description('SQL administrator login.')
param sqlAdminLogin string

@description('SQL administrator password. Never commit this; pass it at deploy time.')
@secure()
param sqlAdminPassword string

@description('Container image to run. Pin a sha tag in production rather than latest.')
param containerImage string = 'ghcr.io/damianpawski/ironbell:latest'

@description('ASPNETCORE_ENVIRONMENT for the running container.')
param aspNetCoreEnvironment string = 'Production'

var sqlServerName = '${namePrefix}-sql-${uniqueString(resourceGroup().id)}'
var databaseName = 'ironbell'
var logAnalyticsName = '${namePrefix}-logs'
var environmentName = '${namePrefix}-env'
var containerAppName = '${namePrefix}-api'

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    // The container and the migration step both reach the database over the public endpoint.
    // Private networking would mean a VNet, which Container Apps consumption cannot use for free.
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    // 32 GB is the free offer's ceiling for data.
    maxSizeBytes: 34359738368
    // Serverless: the database pauses when idle, which is what keeps the vCore-second budget
    // intact. The first request after a pause pays a cold start; that is accepted through M6.
    autoPauseDelay: 60
    minCapacity: json('0.5')
    useFreeLimit: true
    // AutoPause rather than BillOverUsage. Exhausting the monthly allowance makes the database
    // unavailable until the 1st rather than silently starting to charge. That is the right default
    // for a personal project: an outage is recoverable, a surprise invoice is not. Flip this
    // deliberately if availability ever matters more than the bill.
    freeLimitExhaustionBehavior: 'AutoPause'
    zoneRedundant: false
    // Case-insensitive, the SQL Server default. Uniqueness never relies on it -- normalised
    // lowercase columns do that -- precisely so PostgreSQL behaves identically. See ADR 0001.
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// Container Apps has no stable outbound IP on the consumption plan, so the Azure-services rule is
// the only workable option. The migration step opens a temporary rule for the runner and removes
// it again, rather than leaving the world able to connect.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ---------------------------------------------------------------------------
// Compute
// ---------------------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    // Serilog is the real log story; this exists so `az containerapp logs` works at all. Short
    // retention keeps it inside the free grant.
    retentionInDays: 30
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
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

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        // Container Apps terminates TLS and redirects; the app never serves plaintext publicly.
        allowInsecure: false
      }
      secrets: [
        {
          name: 'connection-string'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
      ]
      // No registry credentials: the GHCR package is public, verified by an anonymous pull.
    }
    template: {
      containers: [
        {
          name: 'api'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: aspNetCoreEnvironment
            }
            {
              name: 'Database__Provider'
              value: 'SqlServer'
            }
            {
              name: 'ConnectionStrings__Ironbell'
              secretRef: 'connection-string'
            }
          ]
          // No probes yet, deliberately. The only health endpoint reads the database, which makes
          // it a readiness signal, not a liveness one: wiring it to liveness would let a brief
          // database pause restart healthy containers and turn a blip into an outage. A
          // dependency-free liveness endpoint comes with the probes that need it.
        }
      ]
      scale: {
        // Scale to zero through M0-M6, as planned. The cost is two stacked cold starts, app and
        // database, on the first request after idle. minReplicas goes to 1 at M7.
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

output applicationUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppName string = containerApp.name
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
