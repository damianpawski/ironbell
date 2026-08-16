#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the EF Core migration bundle.

.DESCRIPTION
    Migrations are applied as a pipeline step and never on application startup, so the deploy needs
    a standalone executable that brings a database up to date without the app being involved. This
    script produces it.

    Framework-dependent by default: ~78 MB against ~155 MB self-contained, and appreciably faster
    to build because no runtime pack has to be restored. The trade is that the runner must already
    have the matching .NET runtime, which the workflow pins with actions/setup-dotnet -- a
    dependency we control directly.

    Note the saving is roughly half, not the near-total one might expect. Even framework-dependent,
    the bundle carries the whole EF Core stack, the Design assembly, and both database providers.
    Migrations are SQL Server only, so Npgsql is dead weight here; splitting the providers apart
    would shrink it further and is worth revisiting if artifact size ever actually hurts.

    Pass -SelfContained when the bundle has to run somewhere the runtime cannot be assumed, such as
    a minimal container step or a machine that is not ours.

    Kept as a script rather than inlined into the workflow so local runs and CI cannot drift.

.PARAMETER Runtime
    Target runtime identifier. linux-x64 matches the deploy job; use win-x64 to run it locally.

.PARAMETER Output
    Path of the bundle to produce.

.PARAMETER SelfContained
    Embed the .NET runtime in the bundle. Large and slow to build; only needed where no runtime is
    installed.

.EXAMPLE
    ./scripts/build-migration-bundle.ps1
    ./scripts/build-migration-bundle.ps1 -Runtime win-x64 -Output artifacts/efbundle.exe
    ./scripts/build-migration-bundle.ps1 -SelfContained
#>
[CmdletBinding()]
param(
    [string] $Runtime = 'linux-x64',
    [string] $Output = 'artifacts/efbundle',
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE." }

    # Infrastructure is its own startup project so the design-time factory is used and the API host
    # -- which demands a real connection string -- never has to boot just to scaffold a bundle.
    $arguments = @(
        'ef', 'migrations', 'bundle'
        '--project', 'src/Ironbell.Infrastructure'
        '--startup-project', 'src/Ironbell.Infrastructure'
        '--target-runtime', $Runtime
        '--output', $Output
        '--force'
    )
    if ($SelfContained) { $arguments += '--self-contained' }

    dotnet @arguments

    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations bundle failed with exit code $LASTEXITCODE." }

    $sizeMb = [math]::Round((Get-Item (Join-Path $repositoryRoot $Output)).Length / 1MB, 1)
    $kind = if ($SelfContained) { 'self-contained' } else { 'framework-dependent' }
    Write-Host "Migration bundle written to $Output ($Runtime, $kind, $sizeMb MB)."
}
finally {
    Pop-Location
}
