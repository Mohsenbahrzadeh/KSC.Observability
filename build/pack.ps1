#requires -Version 5.1
<#
.SYNOPSIS
    Restores, tests and packs all KSC.Observability NuGet packages into ./artifacts.

.PARAMETER Version
    Overrides the package version (e.g. 1.2.3). Defaults to the value in Directory.Build.props.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER SkipTests
    Skip the test run.

.EXAMPLE
    ./build/pack.ps1 -Version 1.0.0
#>
param(
    [string]$Version,
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$solution = Join-Path $repoRoot "KSC.Observability.sln"
$artifacts = Join-Path $repoRoot "artifacts"

$versionArgs = @()
if ($Version) { $versionArgs = @("-p:Version=$Version") }

Write-Host "==> Restoring" -ForegroundColor Cyan
dotnet restore $solution

if (-not $SkipTests) {
    Write-Host "==> Testing" -ForegroundColor Cyan
    dotnet test $solution -c $Configuration --no-restore
}

Write-Host "==> Packing into $artifacts" -ForegroundColor Cyan
if (Test-Path $artifacts) { Remove-Item "$artifacts/*.nupkg", "$artifacts/*.snupkg" -ErrorAction SilentlyContinue }
dotnet pack $solution -c $Configuration --no-restore @versionArgs

Write-Host "==> Done. Packages:" -ForegroundColor Green
Get-ChildItem $artifacts -Filter *.nupkg | ForEach-Object { Write-Host "    $($_.Name)" }
