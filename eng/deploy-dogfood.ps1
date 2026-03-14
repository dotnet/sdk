<#
.SYNOPSIS
  Incrementally builds changed SDK projects and deploys DLLs into the dogfood redist layout.
  Much faster than running build.cmd (~seconds vs ~minutes).

.PARAMETER Projects
  Names of projects to build and deploy (without .csproj extension).
  Defaults to the common file-based-programs projects.

.EXAMPLE
  .\eng\deploy-dogfood.ps1
  .\eng\deploy-dogfood.ps1 -Projects dotnet,Microsoft.DotNet.ProjectTools
#>
param(
    [string[]]$Projects = @(
        "dotnet",
        "Microsoft.DotNet.ProjectTools"
    )
)

$ErrorActionPreference = "Stop"
$repoRoot = (Split-Path $PSScriptRoot -Parent)

$dotnet = Join-Path $repoRoot ".dotnet\dotnet"
$sdkVersion = (Get-ChildItem (Join-Path $repoRoot "artifacts\bin\redist\Debug\dotnet\sdk") -Directory | Select-Object -First 1).Name
if (-not $sdkVersion) {
    Write-Error "No SDK version found in redist layout. Run build.cmd first."
    exit 1
}

$redistTargets = @(
    "artifacts\bin\redist\Debug\dotnet\sdk\$sdkVersion",
    "artifacts\bin\redist\Debug\dotnet-installer\sdk\$sdkVersion"
)

foreach($proj in $Projects) {
    # Find the .csproj file.
    $csprojFiles = Get-ChildItem $repoRoot -Recurse -Filter "$proj.csproj" |
        Where-Object { $_.FullName -notmatch "\\test\\" -and $_.FullName -notmatch "\\artifacts\\" }

    if ($csprojFiles.Count -eq 0) {
        Write-Warning "Project '$proj' not found, skipping."
        continue
    }

    $csproj = $csprojFiles[0].FullName
    Write-Host "Building $proj..." -ForegroundColor Cyan

    & $dotnet build $csproj --no-restore -nologo -consoleLoggerParameters:NoSummary 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $proj"
        exit 1
    }

    # Find the built DLL by discovering the TFM folder dynamically.
    $binDir = Join-Path $repoRoot "artifacts\bin\$proj\Debug"
    $builtDll = $null
    if (Test-Path $binDir) {
        $builtDll = Get-ChildItem $binDir -Recurse -Filter "$proj.dll" |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    if (-not $builtDll) {
        Write-Warning "Built DLL not found for $proj under $binDir, skipping deploy."
        continue
    }
    $builtDll = $builtDll.FullName

    # Copy to all redist targets.
    foreach ($target in $redistTargets) {
        $targetPath = Join-Path $repoRoot $target
        $destDll = Join-Path $targetPath "$proj.dll"
        if (Test-Path $destDll) {
            Copy-Item $builtDll $destDll -Force
            # Also copy PDB if available.
            $builtPdb = [IO.Path]::ChangeExtension($builtDll, ".pdb")
            $destPdb = [IO.Path]::ChangeExtension($destDll, ".pdb")
            if (Test-Path $builtPdb) {
                Copy-Item $builtPdb $destPdb -Force
            }
            Write-Host "  Deployed to $target" -ForegroundColor Green
        }
    }

    # Also check dotnet-watch tool location.
    foreach ($target in $redistTargets) {
        $watchToolDir = Join-Path $repoRoot "$target\DotnetTools\dotnet-watch\$sdkVersion\tools"
        if (Test-Path $watchToolDir) {
            $watchDll = Get-ChildItem $watchToolDir -Recurse -Filter "$proj.dll" | Select-Object -First 1
            if ($watchDll) {
                Copy-Item $builtDll $watchDll.FullName -Force
                Write-Host "  Deployed to dotnet-watch in $target" -ForegroundColor Green
            }
        }
    }
}

Write-Host "`nDone! Dogfood SDK updated." -ForegroundColor Green
