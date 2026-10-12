# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkRoot,
    [Parameter(Mandatory)][ValidateSet('Debug', 'Release')][string] $Configuration,
    [Parameter(Mandatory)][string] $BuildOutputPath
)

$ErrorActionPreference = 'Stop'
$SdkRoot = (Resolve-Path -LiteralPath $SdkRoot).Path
$redist = [IO.Path]::GetFullPath((Join-Path $SdkRoot "artifacts\bin\redist\$Configuration"))
$layout = Join-Path $redist 'dotnet'
$output = [IO.Path]::GetFullPath($BuildOutputPath, $SdkRoot).TrimEnd([char[]]"\/")
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not [string]::Equals([IO.Path]::GetDirectoryName($output), $redist, $comparison) -or
    [string]::Equals($output, $layout, $comparison)) {
    throw 'SDK staging output must be a sibling of the shipping dotnet layout, not the layout or its parent.'
}
$dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
if (-not (Test-Path -LiteralPath (Join-Path $layout $dotnetName) -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $layout 'sdk') -PathType Container)) {
    throw 'A complete shipping layout must exist before discarding SDK intermediates.'
}
$targets = @((Join-Path $SdkRoot 'artifacts\obj'), $output)
$parents = @($SdkRoot, (Join-Path $SdkRoot 'artifacts'), (Join-Path $SdkRoot 'artifacts\bin'),
    (Join-Path $SdkRoot 'artifacts\bin\redist'), $redist, $layout)
foreach ($path in $parents + $targets) {
    $item = Get-Item -LiteralPath $path -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to discard SDK intermediates through a linked or non-directory path: $path"
    }
}
foreach ($path in $targets) {
    if (@(Get-ChildItem -LiteralPath $path -Recurse -Force -Attributes ReparsePoint).Count) {
        throw "Refusing to discard SDK intermediates containing links: $path"
    }
}
foreach ($link in Get-ChildItem -LiteralPath $layout -Recurse -Force -Attributes ReparsePoint) {
    $resolved = $link.ResolveLinkTarget($true)
    if ($null -eq $resolved) { throw "Unable to resolve shipping layout link: $($link.FullName)" }
    $target = $resolved.FullName
    foreach ($path in $targets) {
        if ([string]::Equals($target, $path, $comparison) -or
            $target.StartsWith($path + [IO.Path]::DirectorySeparatorChar, $comparison)) {
            throw "The shipping layout still links to SDK intermediates: $($link.FullName)"
        }
    }
}

# Validate every path before deleting either directory.
foreach ($path in $targets) {
    Remove-Item -LiteralPath $path -Recurse -Force
    Write-Host "Discarded SDK intermediate output: $path"
}
