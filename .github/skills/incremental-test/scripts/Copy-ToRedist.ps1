#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Copies build output DLLs into the redist SDK layout for incremental testing.

.DESCRIPTION
    After building modified projects with `dotnet build`, this script copies their
    output assemblies into the redist SDK layout so that dotnet.Tests can run against them.

.PARAMETER Projects
    One or more project names whose output DLLs should be copied.
    Use the directory name under artifacts/bin/ (e.g., "Microsoft.DotNet.Cli.Utils", "dotnet").

.EXAMPLE
    ./Copy-ToRedist.ps1 Microsoft.DotNet.Cli.Utils dotnet
#>

param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
    [string[]]$Projects
)

$ErrorActionPreference = 'Stop'

$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw "Not inside a git repository." }

$redistSdkBase = Join-Path $repoRoot 'artifacts' 'bin' 'redist' 'Debug' 'dotnet' 'sdk'

if (-not (Test-Path $redistSdkBase)) {
    throw "Redist SDK layout not found at '$redistSdkBase'. Run a full build first (build.cmd / build.sh)."
}

$sdkVersionDir = Get-ChildItem $redistSdkBase -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $sdkVersionDir) {
    throw "No SDK version directory found under '$redistSdkBase'."
}

$targetDir = $sdkVersionDir.FullName
Write-Host "Target SDK directory: $targetDir"

foreach ($project in $Projects) {
    $outputDir = Join-Path $repoRoot 'artifacts' 'bin' $project 'Debug' 'net10.0'

    if (-not (Test-Path $outputDir)) {
        Write-Warning "Build output not found: $outputDir — skipping '$project'."
        continue
    }

    # Find DLLs in the project output
    $dlls = Get-ChildItem $outputDir -Filter '*.dll' -File

    foreach ($dll in $dlls) {
        $targetPath = Join-Path $targetDir $dll.Name

        # Safety: only copy DLLs that already exist in the redist layout
        if (-not (Test-Path $targetPath)) {
            continue
        }

        Copy-Item $dll.FullName $targetPath -Force
        Write-Host "  Copied $($dll.Name)"
    }

    # Copy satellite resource assemblies (e.g., cs/, de/, etc.)
    $cultureDirs = Get-ChildItem $outputDir -Directory | Where-Object {
        Test-Path (Join-Path $_.FullName '*.resources.dll')
    }

    foreach ($cultureDir in $cultureDirs) {
        $targetCultureDir = Join-Path $targetDir $cultureDir.Name
        if (-not (Test-Path $targetCultureDir)) { continue }

        $resourceDlls = Get-ChildItem $cultureDir.FullName -Filter '*.resources.dll' -File
        foreach ($resDll in $resourceDlls) {
            $resTarget = Join-Path $targetCultureDir $resDll.Name
            if (-not (Test-Path $resTarget)) { continue }

            Copy-Item $resDll.FullName $resTarget -Force
            Write-Host "  Copied $($cultureDir.Name)/$($resDll.Name)"
        }
    }
}

Write-Host "Done."
