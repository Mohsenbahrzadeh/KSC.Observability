#requires -Version 5.1
<#
.SYNOPSIS
    Bring up the whole KSC.Observability demo environment with one command:
    builds the self-host app, starts the Prometheus + Grafana stack, runs the app,
    waits until everything is healthy and opens the Grafana dashboard.

.PARAMETER NoDemo
    Only start the Prometheus + Grafana stack (use this in real environments where your
    own IIS apps expose /metrics).

.PARAMETER NoBrowser
    Do not open the browser at the end.

.PARAMETER SkipBuild
    Skip building the self-host demo (use the existing build output).

.PARAMETER DemoPort
    Port the self-host demo listens on. Default 9184 (matches deploy/prometheus/prometheus.yml).

.EXAMPLE
    .\up.ps1
.EXAMPLE
    .\up.ps1 -NoDemo          # only the monitoring stack
#>
[CmdletBinding()]
param(
    [switch]$NoDemo,
    [switch]$NoBrowser,
    [switch]$SkipBuild,
    [int]$DemoPort = 9184
)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$deploy  = Join-Path $root 'deploy'
$compose = Join-Path $deploy 'docker-compose.yml'
$runDir  = Join-Path $root '.run'
$demoExe = Join-Path $root 'samples\KSC.Sample.SelfHost\bin\Release\net472\KSC.Sample.SelfHost.exe'
$demoProj= Join-Path $root 'samples\KSC.Sample.SelfHost\KSC.Sample.SelfHost.csproj'
New-Item -ItemType Directory -Force $runDir | Out-Null

# Prefer the 64-bit .NET host; the machine may have a broken x86 dotnet first on PATH.
$dotnet = if (Test-Path "$env:ProgramFiles\dotnet\dotnet.exe") { "$env:ProgramFiles\dotnet\dotnet.exe" } else { 'dotnet' }

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Test-Url($url) {
    try { (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3).StatusCode -ge 200 }
    catch { $false }
}
function Wait-Url($url, $name, $timeoutSec = 90) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        if (Test-Url $url) { Write-Host "    $name is ready" -ForegroundColor Green; return $true }
        Start-Sleep -Seconds 2
    }
    Write-Warning "    $name did not become ready within ${timeoutSec}s ($url)"
    return $false
}

# --- 1. Docker must be running -------------------------------------------------
Write-Step 'Checking Docker'
try { $null = & docker version --format '{{.Server.Version}}' 2>$null; if ($LASTEXITCODE -ne 0) { throw } }
catch { throw "Docker engine is not reachable. Start Docker Desktop and run this again." }

# --- 2. Build the self-host demo ----------------------------------------------
if (-not $NoDemo -and -not $SkipBuild) {
    Write-Step 'Building the self-host demo (Release)'
    & $dotnet build $demoProj -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

# --- 3. Start the Prometheus + Grafana stack ----------------------------------
Write-Step 'Starting Prometheus + Grafana (docker compose up -d)'
& docker compose -f $compose up -d
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

# --- 4. Start the demo app ----------------------------------------------------
if (-not $NoDemo) {
    if (Test-Url "http://localhost:$DemoPort/metrics") {
        Write-Step "Demo already running on port $DemoPort"
    }
    else {
        Write-Step "Starting the self-host demo on port $DemoPort"
        if (-not (Test-Path $demoExe)) { throw "Demo executable not found. Run without -SkipBuild first." }
        $proc = Start-Process -FilePath $demoExe -ArgumentList $DemoPort -PassThru -WindowStyle Hidden `
                    -RedirectStandardOutput (Join-Path $runDir 'demo.out.log') `
                    -RedirectStandardError  (Join-Path $runDir 'demo.err.log')
        $proc.Id | Out-File -Encoding ascii (Join-Path $runDir 'demo.pid')
        Wait-Url "http://localhost:$DemoPort/metrics" 'Demo /metrics' 30 | Out-Null
    }
}

# --- 5. Wait for the stack ----------------------------------------------------
Write-Step 'Waiting for services to be healthy'
Wait-Url 'http://localhost:9090/-/ready' 'Prometheus' 60 | Out-Null
Wait-Url 'http://localhost:3000/api/health' 'Grafana'   90 | Out-Null

# --- 6. Summary + browser -----------------------------------------------------
$dashboard = 'http://localhost:3000/d/ksc-observability-overview'
Write-Host ''
Write-Host '======================================================================' -ForegroundColor Green
Write-Host '  KSC.Observability is up' -ForegroundColor Green
Write-Host '======================================================================' -ForegroundColor Green
Write-Host "  Grafana dashboard : $dashboard   (admin / admin)"
Write-Host '  Prometheus        : http://localhost:9090/targets'
if (-not $NoDemo) {
    Write-Host "  Demo app          : http://localhost:$DemoPort/"
    Write-Host "  Demo metrics      : http://localhost:$DemoPort/metrics"
}
Write-Host ''
Write-Host '  Stop everything   : .\down.ps1'
Write-Host ''

if (-not $NoBrowser) { Start-Process $dashboard }
