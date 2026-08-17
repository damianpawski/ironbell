#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deletes every Azure resource Ironbell created.

.DESCRIPTION
    All Ironbell resources live in one resource group, so removing the group removes everything:
    the container app, its environment, the Log Analytics workspace, the SQL server and the
    database. That is the whole point of putting them in one group.

    This is destructive and irreversible. The database goes with it, and there is no backup unless
    you took one. While the only data is a seeded app_info row that is exactly what you want; once
    real training history exists, it is not.

    Lists what will be deleted and asks before doing it, unless -Force is passed.

.PARAMETER ResourceGroup
    The resource group to delete.

.PARAMETER Force
    Skip the confirmation prompt. For unattended use only.

.PARAMETER Wait
    Block until deletion finishes. Off by default because a container app environment can take
    several minutes to disappear, and the billing stops when deletion starts, not when it ends.

.EXAMPLE
    ./scripts/teardown-azure.ps1
    ./scripts/teardown-azure.ps1 -Force -Wait
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = 'ironbell',
    [switch] $Force,
    [switch] $Wait
)

$ErrorActionPreference = 'Stop'

$exists = az group exists --name $ResourceGroup 2>$null
if ($exists -ne 'true') {
    Write-Host "Resource group '$ResourceGroup' does not exist. Nothing to delete."
    exit 0
}

$account = az account show --query '{name:name, id:id}' --output tsv
Write-Host "Subscription : $account"
Write-Host "Group        : $ResourceGroup"
Write-Host ''
Write-Host 'Resources to be deleted:'
az resource list --resource-group $ResourceGroup --query '[].{name:name, type:type}' --output table

if (-not $Force) {
    Write-Host ''
    Write-Warning 'This deletes the database and everything in it. There is no undo.'
    $answer = Read-Host "Type the group name ('$ResourceGroup') to confirm"
    if ($answer -ne $ResourceGroup) {
        Write-Host 'Aborted; nothing was deleted.'
        exit 1
    }
}

$arguments = @('group', 'delete', '--name', $ResourceGroup, '--yes')
if (-not $Wait) { $arguments += '--no-wait' }

az @arguments
if ($LASTEXITCODE -ne 0) { throw "az group delete failed with exit code $LASTEXITCODE." }

if ($Wait) {
    Write-Host "Deleted '$ResourceGroup'."
}
else {
    Write-Host "Deletion of '$ResourceGroup' started. It continues in the background."
    Write-Host "Check with: az group exists --name $ResourceGroup"
}
