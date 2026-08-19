#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Checks a deployed Ironbell environment end to end.

.DESCRIPTION
    Reads the deployment outputs, then proves the two health endpoints disagree in the way they
    should: liveness answers without a database, readiness does not answer until the schema exists.

    Pass -SqlPassword and it will also apply migrations — opening a firewall rule for this machine,
    running the bundle, and removing the rule again even if the migration fails.

    Read-only without -SqlPassword.

.PARAMETER ResourceGroup
    Resource group holding the deployment.

.PARAMETER DeploymentName
    Name of the ARM deployment. Defaults to 'main', which is what `az deployment group create`
    derives from main.bicep.

.PARAMETER SqlPassword
    SQL administrator password. Supplying it opts in to applying migrations.

.EXAMPLE
    ./scripts/verify-azure.ps1
    ./scripts/verify-azure.ps1 -SqlPassword 'the-password-you-chose'
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = 'ironbell',
    [string] $DeploymentName = 'main',
    [string] $SqlAdminLogin = 'ironbelladmin',
    [string] $SqlPassword
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Get-Output([string] $Name) {
    az deployment group show --resource-group $ResourceGroup --name $DeploymentName `
        --query "properties.outputs.$Name.value" --output tsv
}

function Get-Status([string] $Url) {
    try {
        (Invoke-WebRequest -Uri $Url -SkipHttpErrorCheck -TimeoutSec 120).StatusCode
    }
    catch {
        -1
    }
}

Write-Host '--- deployment outputs ---'
$appUrl = Get-Output 'applicationUrl'
$sqlServer = Get-Output 'sqlServerName'
$sqlFqdn = Get-Output 'sqlServerFqdn'

if (-not $appUrl) { throw "No outputs found for deployment '$DeploymentName' in '$ResourceGroup'." }

Write-Host "  app : $appUrl"
Write-Host "  sql : $sqlFqdn"

# The app scales to zero and the database pauses after an hour, so the first request pays both cold
# starts. Thirty seconds or more is normal and not a fault.
Write-Host ''
Write-Host '--- liveness (must answer with no database) ---'
$live = Get-Status "$appUrl/api/health/live"
Write-Host "  GET /api/health/live  -> HTTP $live"
if ($live -ne 200) {
    throw "Liveness returned $live. The container is not serving, so nothing below will be meaningful."
}

Write-Host ''
Write-Host '--- readiness ---'
$ready = Get-Status "$appUrl/api/health/ping"
Write-Host "  GET /api/health/ping  -> HTTP $ready"

if ($ready -eq 500 -and -not $SqlPassword) {
    Write-Host ''
    Write-Host '500 is correct before migrations run: the schema is applied by the pipeline, never'
    Write-Host 'by the app starting. Re-run with -SqlPassword to apply it now.'
}

if ($SqlPassword) {
    $ruleName = "verify-$(Get-Random)"
    $myIp = (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 30).Trim()

    Write-Host ''
    Write-Host "--- applying migrations (firewall rule $ruleName for $myIp) ---"

    az sql server firewall-rule create --resource-group $ResourceGroup --server $sqlServer `
        --name $ruleName --start-ip-address $myIp --end-ip-address $myIp --output none

    try {
        Push-Location $repositoryRoot
        & "$PSScriptRoot/build-migration-bundle.ps1" -Runtime win-x64 -Output artifacts/efbundle.exe
        if ($LASTEXITCODE -ne 0) { throw "Building the migration bundle failed." }

        # Connection Timeout is generous on purpose: a paused serverless database takes its time.
        $connection = "Server=tcp:$sqlFqdn,1433;Initial Catalog=ironbell;User ID=$SqlAdminLogin;" +
                      "Password=$SqlPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

        & "$repositoryRoot/artifacts/efbundle.exe" --connection $connection
        if ($LASTEXITCODE -ne 0) { throw "Applying migrations failed." }
    }
    finally {
        Pop-Location
        # Removed whether or not the migration worked; a failure must not leave this address allowed.
        az sql server firewall-rule delete --resource-group $ResourceGroup --server $sqlServer `
            --name $ruleName --output none
        Write-Host "Firewall rule $ruleName removed."
    }

    Write-Host ''
    Write-Host '--- readiness, after migrations ---'
    $body = Invoke-RestMethod -Uri "$appUrl/api/health/ping" -TimeoutSec 120
    Write-Host "  status         : $($body.status)"
    Write-Host "  schemaVersion  : $($body.schemaVersion)"
    Write-Host "  server utc     : $($body.utc)"

    if ($body.schemaVersion -ne 'm0') {
        throw "Expected schemaVersion 'm0' but got '$($body.schemaVersion)'."
    }
}

Write-Host ''
Write-Host "Open $appUrl in a browser for the walking-skeleton screen."
