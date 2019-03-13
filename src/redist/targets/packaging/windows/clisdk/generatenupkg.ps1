# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$SdkBundlePath,
    [Parameter(Mandatory=$true)][string]$NugetVersion,
    [Parameter(Mandatory=$true)][string]$NuspecFile,
    [Parameter(Mandatory=$true)][string]$NupkgFile
)

$RepoRoot = Convert-Path "$PSScriptRoot\..\..\..\..\..\.."
$NuGetDir = Join-Path $RepoRoot "artifacts\Tools\nuget"
$NuGetExe = Join-Path $NuGetDir "nuget.exe"
$OutputDirectory = [System.IO.Path]::GetDirectoryName($NupkgFile)
$SdkBundlePath = [System.IO.Path]::GetFullPath($SdkBundlePath)

if (-not (Test-Path $NuGetDir)) {
    New-Item -ItemType Directory -Force -Path $NuGetDir | Out-Null
}

if (-not (Test-Path $NuGetExe)) {
    # Using 3.5.0 to workaround https://github.com/NuGet/Home/issues/5016
    Write-Output "Downloading nuget.exe to $NuGetExe"
    wget https://dist.nuget.org/win-x86-commandline/v3.5.0/nuget.exe -OutFile $NuGetExe
}

if (Test-Path $NupkgFile) {
    Remove-Item -Force $NupkgFile
}

& $NuGetExe pack $NuspecFile -Version $NugetVersion -OutputDirectory $OutputDirectory -NoDefaultExcludes -NoPackageAnalysis -Properties DOTNET_BUNDLE=$SdkBundlePath
Exit $LastExitCode
