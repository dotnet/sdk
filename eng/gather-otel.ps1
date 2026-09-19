<#
.SYNOPSIS
    Runs a dotnet CLI command 10 times (5 non-AOT, then 5 AOT) and exports OTel
    telemetry to a collector via OTLP.

.DESCRIPTION
    gather-otel.ps1 sets up the environment required to capture .NET SDK CLI
    telemetry, then invokes the specified command against the locally-built
    redist dotnet (artifacts\bin\redist\Debug\dotnet) with PWD fixed to
    artifacts\bin\redist\Debug. Each invocation's stdout and stderr are
    written to separate log files under artifacts\tmp\otel\runs\<timestamp>\.

    Run sequence:
      1-5   DOTNET_CLI_ENABLEAOT=0  (non-AOT managed CLI)
      6-10  DOTNET_CLI_ENABLEAOT=1  (AOT native CLI)

    Prerequisites:
      - Build the repo first: .\build.cmd
      - An OTLP-capable collector must be listening (e.g. the Aspire dashboard
        or a standalone otel-collector). Set OTEL_EXPORTER_OTLP_ENDPOINT to
        override the default of http://127.0.0.1:4317.

.PARAMETER CommandArgs
    The dotnet CLI command and arguments to run, e.g. 'dev-certs https'.

.EXAMPLE
    .\eng\gather-otel.ps1 dev-certs https

    Runs 'dotnet dev-certs https' 10 times (5 non-AOT + 5 AOT) and exports
    telemetry to http://127.0.0.1:4317.

.EXAMPLE
    $env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://127.0.0.1:18889'
    .\eng\gather-otel.ps1 dev-certs https

    Same as above but targets a non-default OTLP endpoint.

.EXAMPLE
    .\eng\gather-otel.ps1 --help

    Displays this help text.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true, Mandatory = $true)]
    [string[]]$CommandArgs
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$commandWorkingDir = Join-Path $repoRoot "artifacts\bin\redist\Debug"
$dotnetRoot = Join-Path $repoRoot "artifacts\bin\redist\Debug\dotnet"
$dotnetExe = Join-Path $dotnetRoot "dotnet.exe"

if (-not (Test-Path $commandWorkingDir)) {
    throw "Expected command working directory at $commandWorkingDir. Build the repo first (for example: .\\build.cmd)."
}

if (-not (Test-Path $dotnetExe)) {
    $dotnetFullPath = [System.IO.Path]::GetFullPath($dotnetExe)
    $dotnetRelPath = [System.IO.Path]::GetRelativePath((Get-Location).Path, $dotnetFullPath)
    throw "Expected redist dotnet at $dotnetRelPath. Build the repo first (for example: .\\build.cmd)."
}

$otelStateDir = Join-Path $repoRoot "artifacts\tmp\otel"
New-Item -ItemType Directory -Path $otelStateDir -Force | Out-Null

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runLogsDir = Join-Path $otelStateDir (Join-Path "runs" $runId)
New-Item -ItemType Directory -Path $runLogsDir -Force | Out-Null

function Get-RelativeDisplayPath([string]$Path) {
    $fullPath = (Resolve-Path -Path $Path).Path
    return [System.IO.Path]::GetRelativePath((Get-Location).Path, $fullPath)
}

function Get-FileUri([string]$Path) {
    $fullPath = (Resolve-Path -Path $Path).Path
    return ([System.Uri]::new($fullPath)).AbsoluteUri
}

function Get-Hyperlink([string]$Path) {
    $esc = [char]27
    $bel = [char]7
    $uri = Get-FileUri -Path $Path
    $text = Get-RelativeDisplayPath -Path $Path
    return "$esc]8;;$uri$bel$text$esc]8;;$bel"
}

function Write-VtProgress([int]$Completed, [int]$Total, [string]$Label) {
    $width = 24
    $filled = [Math]::Floor(($Completed * $width) / $Total)
    $bar = ("#" * $filled).PadRight($width, '-')
    $esc = [char]27
    Write-Host -NoNewline "`r$esc[2K$esc[36m[$bar]$esc[0m $Completed/$Total $Label"
}

function Complete-VtProgressLine {
    Write-Host ""
}

function Invoke-LoggedDotnetCommand([string]$Mode, [int]$Iteration) {
    $stdoutLog = Join-Path $runLogsDir ("{0}-{1:00}.stdout.log" -f $Mode, $Iteration)
    $stderrLog = Join-Path $runLogsDir ("{0}-{1:00}.stderr.log" -f $Mode, $Iteration)

    Push-Location $commandWorkingDir
    try {
        & $dotnetExe @CommandArgs 1> $stdoutLog 2> $stderrLog
    }
    finally {
        Pop-Location
    }

    $exitCode = $LASTEXITCODE

    return [PSCustomObject]@{
        ExitCode = $exitCode
        StdoutLog = $stdoutLog
        StderrLog = $stderrLog
    }
}

# Ensure telemetry is enabled and OTLP export is active for this process tree.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "0"
$env:DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER = "1"

if ([string]::IsNullOrWhiteSpace($env:OTEL_EXPORTER_OTLP_ENDPOINT)) {
    $env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://127.0.0.1:4317"
}

Write-Host "Telemetry env: DOTNET_CLI_TELEMETRY_OPTOUT=$($env:DOTNET_CLI_TELEMETRY_OPTOUT), DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=$($env:DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER)"
Write-Host "OTLP endpoint: $($env:OTEL_EXPORTER_OTLP_ENDPOINT)"
Write-Host "Running command: $(Get-Hyperlink -Path $dotnetExe) $($CommandArgs -join ' ')"
Write-Host "Command working directory: $(Get-Hyperlink -Path $commandWorkingDir)"
Write-Host "Command logs directory: $(Get-Hyperlink -Path $runLogsDir)"

$failures = @()
$totalRuns = 10
$runIndex = 0

for ($i = 1; $i -le 5; $i++) {
    $runIndex++
    Write-VtProgress -Completed $runIndex -Total $totalRuns -Label "non-AOT $i/5"
    $env:DOTNET_CLI_ENABLEAOT = "0"
    $result = Invoke-LoggedDotnetCommand -Mode "non-aot" -Iteration $i
    if ($result.ExitCode -ne 0) {
        $failures += [PSCustomObject]@{
            Mode = "non-aot"
            Iteration = $i
            ExitCode = $result.ExitCode
            StdoutLog = $result.StdoutLog
            StderrLog = $result.StderrLog
        }
    }
}

for ($i = 1; $i -le 5; $i++) {
    $runIndex++
    Write-VtProgress -Completed $runIndex -Total $totalRuns -Label "AOT $i/5"
    $env:DOTNET_CLI_ENABLEAOT = "1"
    $result = Invoke-LoggedDotnetCommand -Mode "aot" -Iteration $i
    if ($result.ExitCode -ne 0) {
        $failures += [PSCustomObject]@{
            Mode = "aot"
            Iteration = $i
            ExitCode = $result.ExitCode
            StdoutLog = $result.StdoutLog
            StderrLog = $result.StderrLog
        }
    }
}

Complete-VtProgressLine

if ($failures.Count -eq 0) {
    Write-Host "Completed all runs successfully."
    exit 0
}

Write-Host ""
Write-Host "Run summary: $($failures.Count) command(s) failed."
foreach ($failure in $failures) {
    Write-Host "- $($failure.Mode) $($failure.Iteration)/5 failed with exit code $($failure.ExitCode)"
    Write-Host "  stdout: $(Get-Hyperlink -Path $failure.StdoutLog)"
    Write-Host "  stderr: $(Get-Hyperlink -Path $failure.StderrLog)"
}

exit 1
