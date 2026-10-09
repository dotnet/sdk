# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ManifestsPath,
    [Parameter(Mandatory = $true)][string] $AssetManifestsPath,
    [Parameter(Mandatory = $true)][string] $OfficialBuildId,
    [string] $runtimeSourceFeed,
    [string] $runtimeSourceFeedKey
)

$ErrorActionPreference = 'Stop'
$ci = $true
$restore = $true
$binaryLog = $true
$msbuildEngine = 'dotnet'
$disableConfigureToolsetImport = $true
. "$PSScriptRoot\..\common\tools.ps1"

$arcadeToolset = Split-Path (GetSdkTaskProject 'PublishBuildAssets') -Parent
$project = Join-Path $PSScriptRoot 'RegisterCandidates.proj'
$properties = @(
    "/p:DotnetupArcadeToolsetDir=$arcadeToolset\",
    "/p:DirectoryBuildPropsPath=$arcadeToolset\Directory.Build.props",
    "/p:DirectoryBuildTargetsPath=$arcadeToolset\Directory.Build.targets",
    "/p:DirectoryPackagesPropsPath=$arcadeToolset\Directory.Packages.props",
    "/p:BaseIntermediateOutputPath=$ToolsetDir\DotnetupCandidates\",
    "/p:RepoRoot=$RepoRoot",
    "/p:ManifestsPath=$ManifestsPath",
    "/p:AssetManifestsPath=$AssetManifestsPath",
    "/p:OfficialBuildId=$OfficialBuildId",
    '/p:MaestroApiEndpoint=https://maestro.dot.net'
)

MSBuild $project /t:Restore "/bl:$LogDir\RegisterDotnetupCandidates.Restore.binlog" @properties
MSBuild $project /t:RegisterCandidates "/bl:$LogDir\RegisterDotnetupCandidates.binlog" @properties
