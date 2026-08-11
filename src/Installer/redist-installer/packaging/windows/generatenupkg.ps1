# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$BinDir,
    [Parameter(Mandatory=$true)][string]$ContentPath,
    [Parameter(Mandatory=$true)][string]$NugetVersion,
    [Parameter(Mandatory=$true)][string]$NuspecFile,
    [Parameter(Mandatory=$true)][string]$NupkgFile,
    [Parameter(Mandatory=$false)][string]$Architecture,
    [Parameter(Mandatory=$false)][string]$MmVersion
)

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bOR [Net.SecurityProtocolType]::Tls12

$NuGetDir = Join-Path $BinDir  "nuget"
$NuGetExe = Join-Path $NuGetDir "nuget.exe"
$OutputDirectory = [System.IO.Path]::GetDirectoryName($NupkgFile)
$ContentPath = [System.IO.Path]::GetFullPath($ContentPath)
if ($CabPath) {
    $CabPath = [System.IO.Path]::GetFullPath($CabPath)
}

if (-not (Test-Path $NuGetDir)) {
    New-Item -ItemType Directory -Force -Path $NuGetDir | Out-Null
}

if (-not (Test-Path $NuGetExe)) {
    # Using 3.5.0 to workaround https://github.com/NuGet/Home/issues/5016
    Write-Output "Downloading nuget.exe to $NuGetExe"
    wget https://dist.nuget.org/win-x86-commandline/v3.5.0/nuget.exe -OutFile $NuGetExe
}

if (-not (Test-Path $NuGetExe)) {
    Write-Error "Could not download nuget.exe"
    Exit 1
}

if (Test-Path $NupkgFile) {
    Remove-Item -Force $NupkgFile
}

& $NuGetExe pack $NuspecFile -Version $NugetVersion -OutputDirectory $OutputDirectory -NoDefaultExcludes -NoPackageAnalysis -Properties PAYLOAD_FILES=$ContentPath`;ARCH=$Architecture`;MAJOR_MINOR=$MmVersion
Exit $LastExitCode
