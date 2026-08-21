#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts a standalone Aspire Dashboard to receive OpenTelemetry (OTLP) data from the .NET CLI,
    and prints the OTLP endpoint + UI login URL that Measure-AotStartup.ps1 / `aspire otel` consume.

.DESCRIPTION
    Prefers the `aspire` CLI (`aspire dashboard run`) when present; otherwise falls back to the
    official container image (`mcr.microsoft.com/dotnet/aspire-dashboard`). Either way the dashboard
    exposes:
      * UI            http://localhost:18888
      * OTLP/gRPC     http://localhost:4317   (point the CLI's OTEL_EXPORTER_OTLP_ENDPOINT here)
      * OTLP/HTTP     http://localhost:4318

    The .NET CLI starts exporting traces+metrics over OTLP when DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=1
    (or any standard OTEL_EXPORTER_OTLP_* var) is set - see references/perf-and-otel.md.

.PARAMETER UseDocker
    Force the container fallback even if the `aspire` CLI is installed.

.PARAMETER OtlpGrpcPort
    Host port to map to the dashboard's OTLP/gRPC receiver (default: 4317).

.PARAMETER UiPort
    Host port for the dashboard UI (default: 18888).

.NOTES
    Run this in its own terminal (it blocks) or as a background/detached process. With the `aspire`
    CLI, copy the printed `http://localhost:18888/login?t=<token>` URL: pass it to `aspire otel`
    via --dashboard-url and the browser token is exchanged for an API key automatically.
#>
[CmdletBinding()]
param(
    [switch]$UseDocker,
    [int]$OtlpGrpcPort = 4317,
    [int]$UiPort = 18888
)

$ErrorActionPreference = 'Stop'

$aspire = Get-Command aspire -ErrorAction SilentlyContinue
if ($aspire -and -not $UseDocker) {
    Write-Host '==> Starting Aspire Dashboard via the aspire CLI...' -ForegroundColor Cyan
    Write-Host "    UI:        http://localhost:$UiPort"
    Write-Host "    OTLP/gRPC: http://localhost:$OtlpGrpcPort"
    Write-Host '    Copy the printed login URL (with ?t=<token>) for `aspire otel --dashboard-url`.' -ForegroundColor Yellow
    Write-Host ''
    & aspire dashboard run --frontend-url "http://localhost:$UiPort"
    return
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw 'Neither the `aspire` CLI nor `docker` is available. Install the Aspire CLI (`dotnet tool install -g aspire.cli` / winget) or Docker Desktop.'
}

Write-Host '==> Starting Aspire Dashboard via container image...' -ForegroundColor Cyan
Write-Host "    UI:        http://localhost:$UiPort"
Write-Host "    OTLP/gRPC: http://localhost:$OtlpGrpcPort"
Write-Host '    Anonymous access is enabled for local dev (no token required).' -ForegroundColor Yellow
Write-Host ''
& docker run --rm -it `
    -p "${UiPort}:18888" `
    -p "${OtlpGrpcPort}:18889" `
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
    --name aspire-dashboard `
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
