#requires -Version 5.1
<#
.SYNOPSIS
    Stop the KSC.Observability demo environment: the self-host app and the
    Prometheus + Grafana stack.

.PARAMETER Volumes
    Also delete the Prometheus/Grafana data volumes (wipes stored metrics & dashboards state).
#>
[CmdletBinding()]
param(
    [switch]$Volumes
)

$ErrorActionPreference = 'SilentlyContinue'
$root    = $PSScriptRoot
$compose = Join-Path $root 'deploy\docker-compose.yml'
$pidFile = Join-Path $root '.run\demo.pid'

Write-Host '==> Stopping the self-host demo' -ForegroundColor Cyan
if (Test-Path $pidFile) {
    $demoPid = Get-Content $pidFile | Select-Object -First 1
    if ($demoPid) { Stop-Process -Id ([int]$demoPid) -Force -ErrorAction SilentlyContinue }
    Remove-Item $pidFile -Force
}
# Belt and braces: kill any stray instance.
Get-Process KSC.Sample.SelfHost -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host '==> Stopping Prometheus + Grafana' -ForegroundColor Cyan
if ($Volumes) { & docker compose -f $compose down -v }
else          { & docker compose -f $compose down }

Write-Host '==> Done.' -ForegroundColor Green
