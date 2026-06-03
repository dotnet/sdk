#!/usr/bin/env pwsh
# Creates a 'current' junction/symlink pointing to the single TFM output directory.
# Used by the VS Code launch config so the debugger finds the binary without a TFM prompt.

param(
    [string]$BinDir = (Join-Path $PSScriptRoot '..' '..' '..' 'artifacts' 'bin' 'dotnetup' 'Debug')
)

$BinDir = Resolve-Path $BinDir
$tfmDirs = @(Get-ChildItem -Directory $BinDir -Filter 'net*')

if ($tfmDirs.Count -eq 1) {
    $link = Join-Path $BinDir 'current'
    if (Test-Path $link) { Remove-Item $link -Force -Recurse }
    $linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
    New-Item -ItemType $linkType -Path $link -Target $tfmDirs[0].FullName | Out-Null
    Write-Host "Linked current -> $($tfmDirs[0].Name)"
}
elseif ($tfmDirs.Count -gt 1) {
    $names = ($tfmDirs | ForEach-Object { $_.Name }) -join ', '
    Write-Error "Multiple TFM directories found under '$BinDir': $names. Delete the stale one."
    exit 1
}
else {
    Write-Error "No TFM directory found under '$BinDir'. Build first."
    exit 1
}
