#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measures .NET CLI startup performance for two scenarios using the redist SDK's `dotnet.exe`
    muxer, and (optionally) exports OpenTelemetry spans to a running Aspire Dashboard for a
    span-level breakdown.

.DESCRIPTION
    The redist muxer dispatches to the NativeAOT entrypoint when DOTNET_CLI_ENABLEAOT=true and to
    the managed CLI otherwise, so the default before/after compares the *same* built SDK with the
    AOT path off vs. on. (Pass -BaselineDotnet to instead compare two different builds with
    identical env.)

    Two phases per scenario:
      1. Timing phase - runs `dotnet <Arguments>` Iterations times with the OTLP exporter OFF and
         records wall-clock via Stopwatch (clean numbers, no export/flush overhead). Reports
         min / median / mean / p95.
      2. Telemetry phase (when -OtlpEndpoint is reachable) - a few runs with the OTLP exporter ON,
         tagged OTEL_RESOURCE_ATTRIBUTES="scenario=<label>", so the Aspire Dashboard captures the
         CLI's `dotnet-cli` spans for drill-down. Pull them with:
             aspire otel traces --dashboard-url <url> --format Json --search scenario:<label>

    Writes a Markdown summary suitable for a PR description.

.PARAMETER DotnetPath
    Path to the redist muxer (default: <repo>/artifacts/bin/redist/<Configuration>/dotnet/dotnet.exe).
    Requires a full build.cmd/.sh so the muxer + AOT binary are laid out.

.PARAMETER BaselineDotnet
    Optional second muxer for build-vs-build comparison. When set, both scenarios use identical
    env and the only difference is the executable (labels: base / pr).

.PARAMETER Arguments
    The CLI invocation to benchmark (default: '--version'). Use a low-cost, deterministic command.

.PARAMETER Iterations
    Timed iterations per scenario (default: 30).

.PARAMETER Warmup
    Untimed warmup iterations per scenario (default: 5).

.PARAMETER OtlpEndpoint
    OTLP/gRPC endpoint of a running Aspire Dashboard (default: http://localhost:4317). Pass ''
    to skip the telemetry phase entirely.

.PARAMETER DashboardUrl
    Dashboard UI URL recorded in the report for the `aspire otel` follow-up (default http://localhost:18888).

.PARAMETER OutputPath
    Markdown summary path (default: <repo>/artifacts/aot-startup.md).
#>
[CmdletBinding()]
param(
    [string]$DotnetPath,
    [string]$BaselineDotnet,
    [string]$Arguments = '--version',
    [int]$Iterations = 30,
    [int]$Warmup = 5,
    [string]$Configuration = 'Release',
    [string]$OtlpEndpoint = 'http://localhost:4317',
    [string]$DashboardUrl = 'http://localhost:18888',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Find-RepoRoot {
    $dir = $PSScriptRoot
    while ($dir -and -not (Test-Path (Join-Path $dir '.git'))) {
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    if (-not $dir) { throw "Could not locate the repository root from $PSScriptRoot." }
    return (Resolve-Path $dir).Path
}

function Resolve-Muxer([string]$repoRoot, [string]$config, [string]$explicit) {
    if ($explicit) {
        if (-not (Test-Path $explicit)) { throw "dotnet muxer not found at $explicit." }
        return (Resolve-Path $explicit).Path
    }
    $name = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $candidate = Join-Path $repoRoot "artifacts/bin/redist/$config/dotnet/$name"
    if (-not (Test-Path $candidate)) {
        throw "Redist muxer not found at $candidate. Build the full SDK layout first (build.cmd /.sh), or pass -DotnetPath."
    }
    return (Resolve-Path $candidate).Path
}

# Percentile over a sorted double[] using linear interpolation.
function Get-Percentile([double[]]$sorted, [double]$p) {
    if ($sorted.Count -eq 1) { return $sorted[0] }
    $rank = ($p / 100.0) * ($sorted.Count - 1)
    $lo = [Math]::Floor($rank); $hi = [Math]::Ceiling($rank)
    if ($lo -eq $hi) { return $sorted[[int]$lo] }
    return $sorted[[int]$lo] + ($rank - $lo) * ($sorted[[int]$hi] - $sorted[[int]$lo])
}

function Invoke-Timed([string]$exe, [string]$argline, [hashtable]$env, [int]$count, [int]$warmup) {
    $applied = @{}
    foreach ($k in $env.Keys) { $applied[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $env[$k]) }
    try {
        $argv = $argline.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
        for ($i = 0; $i -lt $warmup; $i++) { & $exe @argv *> $null }
        $times = New-Object System.Collections.Generic.List[double]
        for ($i = 0; $i -lt $count; $i++) {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            & $exe @argv *> $null
            $sw.Stop()
            $times.Add($sw.Elapsed.TotalMilliseconds)
        }
        return $times.ToArray()
    }
    finally {
        foreach ($k in $applied.Keys) { [Environment]::SetEnvironmentVariable($k, $applied[$k]) }
    }
}

$repoRoot = Find-RepoRoot
if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot 'artifacts/aot-startup.md' }
$primary = Resolve-Muxer $repoRoot $Configuration $DotnetPath

# Define scenarios.
$scenarios = @()
if ($BaselineDotnet) {
    $baseMux = Resolve-Muxer $repoRoot $Configuration $BaselineDotnet
    $scenarios += [pscustomobject]@{ Label = 'base'; Exe = $baseMux; Env = @{ DOTNET_CLI_ENABLEAOT = 'true' } }
    $scenarios += [pscustomobject]@{ Label = 'pr'; Exe = $primary; Env = @{ DOTNET_CLI_ENABLEAOT = 'true' } }
    $mode = 'build-vs-build (AOT path on both)'
} else {
    $scenarios += [pscustomobject]@{ Label = 'managed'; Exe = $primary; Env = @{ DOTNET_CLI_ENABLEAOT = 'false' } }
    $scenarios += [pscustomobject]@{ Label = 'aot'; Exe = $primary; Env = @{ DOTNET_CLI_ENABLEAOT = 'true' } }
    $mode = 'managed vs AOT (same SDK)'
}

Write-Host "Muxer:      $primary"
Write-Host "Command:    dotnet $Arguments"
Write-Host "Mode:       $mode"
Write-Host "Iterations: $Iterations (warmup $Warmup)"
Write-Host ''

$results = @()
foreach ($s in $scenarios) {
    Write-Host "==> Timing scenario '$($s.Label)'..." -ForegroundColor Cyan
    $env = @{ DOTNET_CLI_TELEMETRY_OPTOUT = '1' } + $s.Env  # disable export during timing
    $samples = Invoke-Timed $s.Exe $Arguments $env $Iterations $Warmup
    $sorted = ($samples | Sort-Object)
    $results += [pscustomobject]@{
        Label  = $s.Label
        Min    = ($sorted | Select-Object -First 1)
        Median = (Get-Percentile $sorted 50)
        Mean   = ($samples | Measure-Object -Average).Average
        P95    = (Get-Percentile $sorted 95)
    }
}

# Telemetry phase: a handful of exporter-on runs to populate the dashboard.
$telemetryNote = ''
if ($OtlpEndpoint) {
    $reachable = $false
    try {
        $uri = [Uri]$OtlpEndpoint
        $tcp = New-Object System.Net.Sockets.TcpClient
        $tcp.Connect($uri.Host, $uri.Port); $reachable = $tcp.Connected; $tcp.Close()
    } catch { $reachable = $false }

    if ($reachable) {
        foreach ($s in $scenarios) {
            Write-Host "==> Exporting spans for scenario '$($s.Label)' to $OtlpEndpoint..." -ForegroundColor Cyan
            $env = @{
                DOTNET_CLI_TELEMETRY_OPTOUT       = '0'
                DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER = '1'
                OTEL_EXPORTER_OTLP_ENDPOINT       = $OtlpEndpoint
                OTEL_EXPORTER_OTLP_PROTOCOL       = 'grpc'
                OTEL_RESOURCE_ATTRIBUTES          = "scenario=$($s.Label)"
            } + $s.Env
            Invoke-Timed $s.Exe $Arguments $env 5 1 | Out-Null
        }
        $telemetryNote = "Spans exported to the Aspire Dashboard, tagged ``scenario=<label>``. Inspect with:`n`n" +
            "```````n" +
            "aspire otel traces --dashboard-url $DashboardUrl --format Table --search scenario:aot`n" +
            "aspire otel traces --dashboard-url $DashboardUrl --format Json  --search scenario:aot -n 5`n" +
            "```````n"
    } else {
        $telemetryNote = "_Telemetry phase skipped: no Aspire Dashboard reachable at ``$OtlpEndpoint``. Start one with Start-AspireDashboard.ps1._"
    }
}

# Build report.
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('## AOT startup performance')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("Command: ``dotnet $Arguments`` &nbsp;|&nbsp; $Iterations iterations ($Warmup warmup) &nbsp;|&nbsp; $mode.")
[void]$sb.AppendLine('Wall-clock measured with the OTLP exporter off (clean numbers); all times in ms.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Scenario | Min | Median | Mean | P95 |')
[void]$sb.AppendLine('| --- | ---: | ---: | ---: | ---: |')
foreach ($r in $results) {
    [void]$sb.AppendLine(("| {0} | {1:N1} | {2:N1} | {3:N1} | {4:N1} |" -f $r.Label, $r.Min, $r.Median, $r.Mean, $r.P95))
}
if ($results.Count -eq 2) {
    $a = $results[0]; $b = $results[1]
    $deltaMed = $b.Median - $a.Median
    $pct = if ($a.Median) { $deltaMed / $a.Median * 100 } else { 0 }
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine(("**Median delta ({0} -> {1}): {2:+0.0;-0.0} ms ({3:+0.0;-0.0}%).**" -f $a.Label, $b.Label, $deltaMed, $pct))
}
[void]$sb.AppendLine('')
if ($telemetryNote) { [void]$sb.AppendLine($telemetryNote) }

Set-Content -Path $OutputPath -Value $sb.ToString() -Encoding utf8
Write-Host ''
Write-Host "Summary written to: $OutputPath" -ForegroundColor Green
$results | Format-Table -AutoSize | Out-Host
