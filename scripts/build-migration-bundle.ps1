#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the EF Core migration bundle.

.DESCRIPTION
    Migrations are applied as a pipeline step and never on application startup, so the deploy needs
    a standalone executable that brings a database up to date without the app being involved. This
    script produces it.

    Self-contained by design: the deploy job then needs no particular .NET runtime installed, which
    keeps applying migrations independent of how the runner happens to be provisioned. The cost is
    a large artifact (~150 MB) that exists only for the length of the job.

    Kept as a script rather than inlined into the workflow so local runs and CI cannot drift.

.PARAMETER Runtime
    Target runtime identifier. linux-x64 matches the deploy job; use win-x64 to run it locally.

.PARAMETER Output
    Path of the bundle to produce.

.EXAMPLE
    ./scripts/build-migration-bundle.ps1
    ./scripts/build-migration-bundle.ps1 -Runtime win-x64 -Output artifacts/efbundle.exe
#>
[CmdletBinding()]
param(
    [string] $Runtime = 'linux-x64',
    [string] $Output = 'artifacts/efbundle'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE." }

    # Infrastructure is its own startup project so the design-time factory is used and the API host
    # -- which demands a real connection string -- never has to boot just to scaffold a bundle.
    dotnet ef migrations bundle `
        --project src/Ironbell.Infrastructure `
        --startup-project src/Ironbell.Infrastructure `
        --target-runtime $Runtime `
        --self-contained `
        --output $Output `
        --force

    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations bundle failed with exit code $LASTEXITCODE." }

    Write-Host "Migration bundle written to $Output ($Runtime)."
}
finally {
    Pop-Location
}
