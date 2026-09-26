#!/usr/bin/env pwsh
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateRange(1, 100)]
    [int]$Iterations = 30,
    [ValidateRange(0, 20)]
    [int]$WarmupIterations = 3,
    [string]$OutputDirectory,
    [switch]$SlnHelpOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

if (-not ($IsWindows -or $env:OS -eq "Windows_NT")) {
    throw "This retained performance harness currently supports Windows only."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"
$productProject = Join-Path $repoRoot "src\Cli\dotnet-aot\dotnet-aot.csproj"
$redistProject = Join-Path $repoRoot "src\Layout\redist\redist.csproj"
$modes = @("Embedded", "ExternalLocalized", "ExternalAll")
$cultures = @("", "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant")
$deepCultures = @("", "fr", "ja", "zh-Hant")
$commands = @(
    [pscustomobject]@{ Name = "version"; Arguments = @("--version") },
    [pscustomobject]@{ Name = "root-help"; Arguments = @() },
    [pscustomobject]@{ Name = "sln-help"; Arguments = @("sln", "--help") }
)
if ($SlnHelpOnly) {
    $commands = @($commands | Where-Object Name -eq "sln-help")
}
$orders = @(
    @("Embedded", "ExternalLocalized", "ExternalAll"),
    @("Embedded", "ExternalAll", "ExternalLocalized"),
    @("ExternalLocalized", "Embedded", "ExternalAll"),
    @("ExternalLocalized", "ExternalAll", "Embedded"),
    @("ExternalAll", "Embedded", "ExternalLocalized"),
    @("ExternalAll", "ExternalLocalized", "Embedded")
)

if (-not $OutputDirectory) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDirectory = Join-Path $repoRoot "artifacts\perf\dotnet-aot-external-resources\$stamp"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$runWorkingDirectory = Join-Path ([IO.Path]::GetTempPath()) "dotnet-aot-resource-mode-work"
New-Item -ItemType Directory -Force -Path $runWorkingDirectory | Out-Null
$modeArtifacts = @{}

function Invoke-Checked([string]$description, [scriptblock]$action) {
    Write-Host $description -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) {
        throw "$description failed with exit code $LASTEXITCODE."
    }
}

if (-not $NoBuild) {
    $existingRedistHost = Join-Path $repoRoot "artifacts\bin\redist\$Configuration\dotnet\dotnet.exe"
    if (Test-Path $existingRedistHost) {
        & $existingRedistHost build-server shutdown | Out-Null
    }

    Invoke-Checked "Building the $Configuration redist SDK" {
        & $dotnet build $redistProject `
            -c $Configuration `
            --no-restore `
            -nodeReuse:false `
            -p:UseSharedCompilation=false `
            -p:_DotnetAotResourceMode=Embedded `
            -p:SkipBuildingInstallers=true `
            -p:SkipBuildingArchives=true
    }

    foreach ($mode in $modes) {
        $modeDirectory = Join-Path $OutputDirectory $mode
        $publishDirectory = Join-Path $modeDirectory "publish"
        New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

        Invoke-Checked "Publishing dotnet-aot in $mode mode" {
            & $dotnet publish $productProject `
                -c $Configuration `
                -r win-x64 `
                --no-restore `
                -p:_DotnetAotResourceMode=$mode `
                -p:PublishDir="$publishDirectory\"
        }

        $nativeIntermediate = Get-ChildItem (Join-Path $repoRoot "artifacts\obj\dotnet-aot\$Configuration") `
                -Directory `
                -Recurse `
                -Filter native |
            Where-Object { $_.Parent.Name -eq "win-x64" } |
            Select-Object -First 1 -ExpandProperty FullName
        if (-not $nativeIntermediate) {
            throw "Could not locate the dotnet-aot native intermediate directory."
        }
        $modeIntermediate = Join-Path $modeDirectory "native"
        New-Item -ItemType Directory -Force -Path $modeIntermediate | Out-Null
        foreach ($name in @("dotnet-aot.ilc.rsp", "dotnet-aot.mstat", "dotnet-aot.scan.dgml.xml", "dotnet-aot.codegen.dgml.xml")) {
            $source = Join-Path $nativeIntermediate $name
            if (Test-Path $source) {
                Copy-Item $source $modeIntermediate -Force
            }
        }

        $inventory = Get-ChildItem (Join-Path $repoRoot "artifacts\obj\dotnet-aot\$Configuration") `
                -Recurse `
                -File `
                -Filter dotnet-aot-resource-mode.txt |
            Where-Object { $_.Directory.Name -eq "win-x64" } |
            Select-Object -First 1 -ExpandProperty FullName
        if ($inventory) {
            Copy-Item $inventory $modeDirectory -Force
        }
    }
}

$redistRoot = Join-Path $repoRoot "artifacts\bin\redist\$Configuration\dotnet"
$hostPath = Join-Path $redistRoot "dotnet.exe"
$sdkRoot = Join-Path $redistRoot "sdk"
$sdkDirectory = Get-ChildItem $sdkRoot -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$nativeTarget = Join-Path $sdkDirectory "dotnet-aot.dll"
$runtimeConfig = Join-Path $sdkDirectory "dotnet.runtimeconfig.json"

if (-not (Test-Path $hostPath) -or -not (Test-Path $sdkDirectory)) {
    throw "The $Configuration redist SDK was not found under '$redistRoot'."
}

foreach ($mode in $modes) {
    $nativeLibrary = Join-Path $OutputDirectory "$mode\publish\dotnet-aot.dll"
    if (-not (Test-Path $nativeLibrary)) {
        throw "The $mode native library was not found at '$nativeLibrary'."
    }

    $file = Get-Item $nativeLibrary
    $modeArtifacts[$mode] = [pscustomobject]@{
        Mode = $mode
        Path = $nativeLibrary
        Bytes = $file.Length
        Sha256 = (Get-FileHash $nativeLibrary -Algorithm SHA256).Hash
    }
}

$originalNative = Join-Path $OutputDirectory "original-dotnet-aot.dll"
Copy-Item $nativeTarget $originalNative -Force

function Invoke-Dotnet(
    [string]$mode,
    [bool]$enableAot,
    [string]$culture,
    [string[]]$arguments,
    [string]$cliHome)
{
    Copy-Item $modeArtifacts[$mode].Path $nativeTarget -Force
    New-Item -ItemType Directory -Force -Path $cliHome | Out-Null

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $hostPath
    $startInfo.WorkingDirectory = $runWorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $arguments) {
        $startInfo.ArgumentList.Add($argument)
    }
    $startInfo.Environment["DOTNET_ROOT"] = $redistRoot
    $startInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    $startInfo.Environment["DOTNET_CLI_ENABLEAOT"] = $enableAot.ToString().ToLowerInvariant()
    $startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    $startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
    $startInfo.Environment["DOTNET_NOLOGO"] = "1"
    $startInfo.Environment["DOTNET_CLI_HOME"] = $cliHome
    if ($culture.Length -eq 0) {
        [void]$startInfo.Environment.Remove("DOTNET_CLI_UI_LANGUAGE")
    }
    else {
        $startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = $culture
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    [System.Threading.Tasks.Task]::WaitAll($stdout, $stderr)
    $stopwatch.Stop()

    [pscustomobject]@{
        Mode = $mode
        EnableAot = $enableAot
        Culture = $culture
        Arguments = $arguments
        ExitCode = $process.ExitCode
        Stdout = $stdout.Result
        Stderr = $stderr.Result
        ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
        CpuMilliseconds = $process.TotalProcessorTime.TotalMilliseconds
        PeakWorkingSetBytes = $process.PeakWorkingSet64
        PrivateBytes = $process.PrivateMemorySize64
    }
}

$functional = [System.Collections.Generic.List[object]]::new()
$performance = [System.Collections.Generic.List[object]]::new()
$expectedExitCodes = @{}

try {
    Write-Host "Running functional culture parity..." -ForegroundColor Cyan
    foreach ($culture in $cultures) {
        foreach ($command in $commands) {
            $managedHome = Join-Path $OutputDirectory "homes\functional\managed\$($command.Name)\$($culture -replace '^$', 'invariant')"
            $managed = Invoke-Dotnet "Embedded" $false $culture $command.Arguments $managedHome
            $expectedExitCodes["$culture|$($command.Name)"] = $managed.ExitCode

            foreach ($mode in $modes) {
                $aotHome = Join-Path $OutputDirectory "homes\functional\$mode\$($command.Name)\$($culture -replace '^$', 'invariant')"
                $aot = Invoke-Dotnet $mode $true $culture $command.Arguments $aotHome
                $equal = $aot.ExitCode -eq $managed.ExitCode `
                    -and $aot.Stdout -ceq $managed.Stdout `
                    -and $aot.Stderr -ceq $managed.Stderr
                $functional.Add([pscustomobject]@{
                    Mode = $mode
                    Culture = $culture
                    Command = $command.Name
                    ExitCode = $aot.ExitCode
                    OutputEqual = $equal
                    Stdout = $aot.Stdout
                    Stderr = $aot.Stderr
                })
                if (-not $equal) {
                    $differenceDirectory = Join-Path $OutputDirectory "differences"
                    New-Item -ItemType Directory -Force -Path $differenceDirectory | Out-Null
                    $differenceName = "$mode-$($command.Name)-$($culture -replace '^$', 'invariant')"
                    $managed.Stdout | Set-Content (Join-Path $differenceDirectory "$differenceName-managed.stdout.txt")
                    $managed.Stderr | Set-Content (Join-Path $differenceDirectory "$differenceName-managed.stderr.txt")
                    $aot.Stdout | Set-Content (Join-Path $differenceDirectory "$differenceName-aot.stdout.txt")
                    $aot.Stderr | Set-Content (Join-Path $differenceDirectory "$differenceName-aot.stderr.txt")
                    throw "$mode $($command.Name) output differed from managed output for culture '$culture'."
                }
            }
        }
    }

    Write-Host "Confirming primary commands do not require managed fallback..." -ForegroundColor Cyan
    $runtimeConfigBackup = "$runtimeConfig.resource-mode-preflight"
    Move-Item $runtimeConfig $runtimeConfigBackup -Force
    try {
        foreach ($mode in $modes) {
            foreach ($command in $commands) {
                $preflightHome = Join-Path $OutputDirectory "homes\preflight\$mode\$($command.Name)"
                $preflight = Invoke-Dotnet $mode $true "" $command.Arguments $preflightHome
                $expectedExitCode = $expectedExitCodes["|$($command.Name)"]
                if ($preflight.ExitCode -ne $expectedExitCode -or $preflight.Stdout.Length -eq 0) {
                    throw "$mode $($command.Name) did not complete in AOT without the managed runtimeconfig."
                }
            }
        }
    }
    finally {
        Move-Item $runtimeConfigBackup $runtimeConfig -Force
    }

    Write-Host "Running balanced fresh-process measurements..." -ForegroundColor Cyan
    foreach ($culture in $deepCultures) {
        foreach ($command in $commands) {
            foreach ($state in @("Warm", "PracticalCold")) {
                foreach ($mode in $modes) {
                    for ($warmup = 0; $warmup -lt $WarmupIterations; $warmup++) {
                        $warmupHome = Join-Path $OutputDirectory "homes\warmup\$state\$mode\$($command.Name)\$($culture -replace '^$', 'invariant')"
                        if ($state -eq "PracticalCold") {
                            $warmupHome = "$warmupHome-$warmup"
                        }
                        [void](Invoke-Dotnet $mode $true $culture $command.Arguments $warmupHome)
                    }
                }

                for ($iteration = 0; $iteration -lt $Iterations; $iteration++) {
                    foreach ($mode in $orders[$iteration % $orders.Count]) {
                        $cliHome = Join-Path $OutputDirectory "homes\measure\$state\$mode\$($command.Name)\$($culture -replace '^$', 'invariant')"
                        if ($state -eq "PracticalCold") {
                            $cliHome = "$cliHome-$iteration"
                        }
                        $result = Invoke-Dotnet $mode $true $culture $command.Arguments $cliHome
                        $expectedExitCode = $expectedExitCodes["$culture|$($command.Name)"]
                        if ($result.ExitCode -ne $expectedExitCode) {
                            throw "$mode $($command.Name) failed during measurement: $($result.Stderr)"
                        }
                        $performance.Add([pscustomobject]@{
                            State = $state
                            Iteration = $iteration + 1
                            Mode = $mode
                            Culture = $culture
                            Command = $command.Name
                            ElapsedMilliseconds = $result.ElapsedMilliseconds
                            CpuMilliseconds = $result.CpuMilliseconds
                            PeakWorkingSetBytes = $result.PeakWorkingSetBytes
                            PrivateBytes = $result.PrivateBytes
                            StdoutSha256 = [Convert]::ToHexString(
                                [Security.Cryptography.SHA256]::HashData(
                                    [Text.Encoding]::UTF8.GetBytes($result.Stdout)))
                        })
                    }
                }
            }
        }
    }
}
finally {
    if (Test-Path "$runtimeConfig.resource-mode-preflight") {
        Move-Item "$runtimeConfig.resource-mode-preflight" $runtimeConfig -Force
    }
    Copy-Item $originalNative $nativeTarget -Force
}

$functional | ConvertTo-Json -Depth 5 |
    Set-Content (Join-Path $OutputDirectory "functional.json") -Encoding utf8
$performance | ConvertTo-Json -Depth 5 |
    Set-Content (Join-Path $OutputDirectory "performance.json") -Encoding utf8
$modeArtifacts.Values | ConvertTo-Json -Depth 5 |
    Set-Content (Join-Path $OutputDirectory "artifacts.json") -Encoding utf8

$summaryRows = foreach ($group in $performance | Group-Object State, Culture, Command, Mode) {
    $values = @($group.Group.ElapsedMilliseconds | Sort-Object)
    [pscustomobject]@{
        State = $group.Group[0].State
        Culture = $group.Group[0].Culture
        Command = $group.Group[0].Command
        Mode = $group.Group[0].Mode
        Count = $values.Count
        MeanMilliseconds = ($values | Measure-Object -Average).Average
        MedianMilliseconds = if ($values.Count % 2 -eq 0) {
            ($values[$values.Count / 2 - 1] + $values[$values.Count / 2]) / 2
        } else {
            $values[[math]::Floor($values.Count / 2)]
        }
        P95Milliseconds = $values[[math]::Ceiling($values.Count * 0.95) - 1]
    }
}

$summaryRows | Export-Csv (Join-Path $OutputDirectory "summary.csv") -NoTypeInformation

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# dotnet-aot resource mode measurement")
$markdown.Add("")
$markdown.Add("- Configuration: $Configuration")
$markdown.Add("- Iterations per case: $Iterations")
$markdown.Add("- Functional cases: $($functional.Count)")
$markdown.Add("- Performance launches: $($performance.Count)")
$markdown.Add("")
$markdown.Add("## Native artifacts")
$markdown.Add("")
$markdown.Add("| Mode | Bytes | SHA-256 |")
$markdown.Add("| --- | ---: | --- |")
foreach ($mode in $modes) {
    $artifact = $modeArtifacts[$mode]
    $markdown.Add("| $mode | $($artifact.Bytes) | ``$($artifact.Sha256)`` |")
}
$markdown.Add("")
$markdown.Add("## Timing summary")
$markdown.Add("")
$markdown.Add("| State | Culture | Command | Mode | Count | Mean ms | Median ms | P95 ms |")
$markdown.Add("| --- | --- | --- | --- | ---: | ---: | ---: | ---: |")
foreach ($row in $summaryRows) {
    $culture = if ($row.Culture.Length -eq 0) { "invariant" } else { $row.Culture }
    $markdown.Add(
        "| $($row.State) | $culture | $($row.Command) | $($row.Mode) | $($row.Count) | " +
        "$($row.MeanMilliseconds.ToString('F3')) | $($row.MedianMilliseconds.ToString('F3')) | " +
        "$($row.P95Milliseconds.ToString('F3')) |")
}
$markdown | Set-Content (Join-Path $OutputDirectory "summary.md") -Encoding utf8

Write-Host "Resource-mode evidence: $OutputDirectory" -ForegroundColor Green
