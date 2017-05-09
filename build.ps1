#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug",
    [string]$Platform="Any CPU",
    [string]$Verbosity="minimal",
    [switch]$SkipTests,
    [switch]$FullMSBuild,
    [switch]$RealSign,
    [switch]$Help,
    [Parameter(Position=0, ValueFromRemainingArguments=$true)]
    $ExtraParameters)

if($Help)
{
    Write-Host "Usage: .\build.ps1 [Options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
    Write-Host "  -Platform <PLATFORM>               Build the specified Platform (Any CPU)"
    Write-Host "  -Verbosity <VERBOSITY>             Build console output verbosity (minimal or diagnostic, default: minimal)"
    Write-Host "  -SkipTests                         Skip executing unit tests"
    Write-Host "  -FullMSBuild                       Run tests with the full .NET Framework version of MSBuild instead of the .NET Core version"
    Write-Host "  -RealSign                          Sign the output DLLs"
    Write-Host "  -Help                              Display this help message"
    Write-Host ""
    Write-Host "Any additional parameters will be passed through to the main invocation of MSBuild"
    exit 0
}

$RepoRoot = "$PSScriptRoot"
$PackagesPath = "$RepoRoot\packages"
$env:NUGET_PACKAGES = $PackagesPath
$DotnetCLIVersion = Get-Content "$RepoRoot\DotnetCLIVersion.txt"

# Use a repo-local install directory (but not the bin directory because that gets cleaned a lot)
if (!$env:DOTNET_INSTALL_DIR)
{
    $env:DOTNET_INSTALL_DIR="$RepoRoot\.dotnet_cli\"
}

if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}

if ($Verbosity -eq 'diagnostic') {
    $dotnetInstallVerbosity = "-Verbose"
}

# Install a stage 0
$DOTNET_INSTALL_SCRIPT_URL="https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.ps1"
Invoke-WebRequest $DOTNET_INSTALL_SCRIPT_URL -OutFile "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1"

& "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1" -Version $DotnetCLIVersion $dotnetInstallVerbosity
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }

# Install 1.0.4 shared framework
if (!(Test-Path "$env:DOTNET_INSTALL_DIR\shared\Microsoft.NETCore.App\1.0.4"))
{
    & "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1" -Channel "Preview" -Version 1.0.4 -SharedRuntime
    if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }
}

# Install 1.1.1 shared framework
if (!(Test-Path "$env:DOTNET_INSTALL_DIR\shared\Microsoft.NETCore.App\1.1.1"))
{
    & "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1" -Channel "Release/1.1.0" -Version 1.1.1 -SharedRuntime
    if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }
}

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

# Disable first run since we want to control all package sources
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

$logPath = "$RepoRoot\bin\log"
if (!(Test-Path -Path $logPath)) {
    New-Item -Path $logPath -Force -ItemType 'Directory' | Out-Null
}

$signType = 'public'
if ($RealSign) {
    $signType = 'real'
}

$buildTarget = 'Build'
if ($SkipTests) {
    $buildTarget = 'BuildWithoutTesting'
}

if ($FullMSBuild)
{
    $env:DOTNET_SDK_TEST_MSBUILD_PATH = join-path $env:VSInstallDir "MSBuild\15.0\bin\MSBuild.exe"
}

$commonBuildArgs = echo $RepoRoot\build\build.proj /m:1 /nologo /p:Configuration=$Configuration /p:Platform=$Platform /p:SignType=$signType /verbosity:$Verbosity

# NET Core Build 
$msbuildSummaryLog = Join-Path -path $logPath -childPath "sdk.log"
$msbuildWarningLog = Join-Path -path $logPath -childPath "sdk.wrn"
$msbuildFailureLog = Join-Path -path $logPath -childPath "sdk.err"
$msbuildBinLog = Join-Path -path $logPath -childPath "sdk.binlog"

dotnet msbuild /t:$buildTarget $commonBuildArgs /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildFailureLog /bl:$msbuildBinLog $ExtraParameters
if($LASTEXITCODE -ne 0) { throw "Failed to build" }

msbuild  /t:$buildTarget $commonBuildArgs /nr:false /p:BuildTemplates=true /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildFailureLog
if($LASTEXITCODE -ne 0) { throw "Failed to build templates" }

# Modern VSIX build
$msbuildSummaryLog = Join-Path -path $logPath -childPath "modernvsix.log"
$msbuildWarningLog = Join-Path -path $logPath -childPath "modernvsix.wrn"
$msbuildFailureLog = Join-Path -path $logPath -childPath "modernvsix.err"

msbuild /t:BuildModernVsixPackages $commonBuildArgs /nr:false /p:BuildTemplates=true /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildFailureLog
if($LASTEXITCODE -ne 0) { throw "Failed to build modern vsix packages" }

