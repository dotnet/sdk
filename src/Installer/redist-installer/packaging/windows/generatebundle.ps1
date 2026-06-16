# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$UpgradePoliciesWxsFile,
    [Parameter(Mandatory=$true)][string]$WorkloadManifestWxsFile,
    [Parameter(Mandatory=$true)][string]$CLISDKMSIFile,
    [Parameter(Mandatory=$true)][string]$ASPNETRuntimeWixLibFile,
    [Parameter(Mandatory=$true)][string]$SharedFxMSIFile,
    [Parameter(Mandatory=$true)][string]$HostFxrMSIFile,
    [Parameter(Mandatory=$true)][string]$SharedHostMSIFile,
    [Parameter(Mandatory=$true)][string]$WinFormsAndWpfMSIFile,
    [Parameter(Mandatory=$true)][string]$NetCoreAppTargetingPackMSIFile,
    [Parameter(Mandatory=$true)][string]$NetStandardTargetingPackMSIFile,
    [Parameter(Mandatory=$true)][string]$NetCoreAppHostPackMSIFile,
    [Parameter(Mandatory=$true)][string]$AlternateNetCoreAppHostPackMSIFile,
    [Parameter(Mandatory=$true)][string]$Arm64NetCoreAppHostPackMSIFile,
    [Parameter(Mandatory=$true)][string]$AspNetTargetingPackMSIFile,
    [Parameter(Mandatory=$true)][string]$WindowsDesktopTargetingPackMSIFile,
    [Parameter(Mandatory=$true)][string]$FinalizerExe,
    [Parameter(Mandatory=$true)][string]$TemplatesMSIFile,
    [Parameter(Mandatory=$true)][string]$DotnetBundleOutput,
    [Parameter(Mandatory=$true)][string]$WixRoot,
    [Parameter(Mandatory=$true)][string]$ProductMoniker,
    [Parameter(Mandatory=$true)][string]$DotnetMSIVersion,
    [Parameter(Mandatory=$true)][string]$SDKBundleVersion,
    [Parameter(Mandatory=$true)][string]$MinimumVSVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLINugetVersion,
    [Parameter(Mandatory=$true)][string]$VersionMajor,
    [Parameter(Mandatory=$true)][string]$VersionMinor,
    [Parameter(Mandatory=$true)][string]$WindowsDesktopVersion,
    [Parameter(Mandatory=$true)][string]$UpgradeCode,
    [Parameter(Mandatory=$true)][string]$DependencyKeyName,
    [Parameter(Mandatory=$true)][string]$Architecture,
    [Parameter(Mandatory=$true)][string]$DotNetRuntimeVersion,
    [Parameter(Mandatory=$true)][string]$AspNetCoreVersion,
    [Parameter(Mandatory=$true)][string]$SDKProductBandVersion
)

function RunCandleForBundle
{
    $result = $true
    pushd "$WixRoot"

    Write-Information "Running candle for bundle.."

    $candleOutput = .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$PSScriptRoot\dummyeula.rtf" `
        -dProductMoniker="$ProductMoniker" `
        -dBuildVersion="$DotnetMSIVersion" `
        -dSDKBundleVersion="$SDKBundleVersion" `
        -dMinimumVSVersion="$MinimumVSVersion" `
        -dSDKProductBandVersion="$SDKProductBandVersion" `
        -dNugetVersion="$DotnetCLINugetVersion" `
        -dVersionMajor="$VersionMajor" `
        -dMajorVersion="$VersionMajor" `
        -dVersionMinor="$VersionMinor" `
        -dMinorVersion="$VersionMinor" `
        -dCLISDKMsiSourcePath="$CLISDKMSIFile" `
        -dDependencyKeyName="$DependencyKeyName" `
        -dUpgradeCode="$UpgradeCode" `
        -dSharedFXMsiSourcePath="$SharedFxMSIFile" `
        -dHostFXRMsiSourcePath="$HostFxrMSIFile" `
        -dSharedHostMsiSourcePath="$SharedHostMSIFile" `
        -dWinFormsAndWpfMsiSourcePath="$WinFormsAndWpfMSIFile" `
        -dNetCoreAppTargetingPackMsiSourcePath="$NetCoreAppTargetingPackMSIFile" `
        -dNetCoreAppHostPackMsiSourcePath="$NetCoreAppHostPackMSIFile" `
        -dAlternateNetCoreAppHostPackMsiSourcePath="$AlternateNetCoreAppHostPackMSIFile" `
        -dArm64NetCoreAppHostPackMsiSourcePath="$Arm64NetCoreAppHostPackMSIFile" `
        -dNetStandardTargetingPackMsiSourcePath="$NetStandardTargetingPackMSIFile" `
        -dAspNetTargetingPackMsiSourcePath="$AspNetTargetingPackMSIFile" `
        -dWindowsDesktopTargetingPackMsiSourcePath="$WindowsDesktopTargetingPackMSIFile" `
        -dFinalizerExeSourcePath="$FinalizerExe" `
        -dTemplatesMsiSourcePath="$TemplatesMSIFile" `
        -dManifestsMsiSourcePath="$ManifestsMSIFile" `
        -dWinFormsAndWpfVersion="$WindowsDesktopVersion" `
        -dAdditionalSharedFXMsiSourcePath="$AdditionalSharedFxMSIFile" `
        -dAdditionalHostFXRMsiSourcePath="$AdditionalHostFxrMSIFile" `
        -dAdditionalSharedHostMsiSourcePath="$AdditionalSharedHostMSIFile" `
        -dDotNetRuntimeVersion="$DotNetRuntimeVersion" `
        -dAspNetCoreVersion="$AspNetCoreVersion" `
        -dLocalizedContentDirs="$LocalizedContentDirs" `
        -arch "$Architecture" `
        -ext WixBalExtension.dll `
        -ext WixUtilExtension.dll `
        -ext WixTagExtension.dll `
        "$AuthWsxRoot\bundle.wxs" "$WorkloadManifestWxsFile" "$UpgradePoliciesWxsFile"

    Write-Information "Candle output: $candleOutput"

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Information "Candle failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunLightForBundle
{
    $result = $true
    pushd "$WixRoot"

    $WorkloadManifestWixobjFile = [System.IO.Path]::GetFileNameWithoutExtension($WorkloadManifestWxsFile) + ".wixobj"
    $UpgradePoliciesWixobjFile = [System.IO.Path]::GetFileNameWithoutExtension($UpgradePoliciesWxsFile) + ".wixobj"

    Write-Information "Running light for bundle.."

    $lightOutput = .\light.exe -nologo `
        -cultures:en-us `
        bundle.wixobj `
        $WorkloadManifestWixobjFile `
        $UpgradePoliciesWixobjFile `
        $ASPNETRuntimeWixlibFile `
        -ext WixBalExtension.dll `
        -ext WixUtilExtension.dll `
        -ext WixTagExtension.dll `
        -b "$AuthWsxRoot" `
        -out $DotnetBundleOutput

    Write-Information "Light output: $lightOutput"

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Information "Light failed with exit code $LastExitCode."
    }

    popd
    return $result
}


if(!(Test-Path $CLISDKMSIFile))
{
    throw "$CLISDKMSIFile not found"
}

if(!(Test-Path $ASPNETRuntimeWixLibFile))
{
    throw "$ASPNETRuntimeWixLibFile not found"
}

if([string]::IsNullOrEmpty($WixRoot))
{
    Exit -1
}

Write-Information "Creating dotnet Bundle at $DotnetBundleOutput"

$AuthWsxRoot = $PSScriptRoot
$LocalizedContentDirs = (Get-ChildItem "$AuthWsxRoot\LCID\*\bundle.wxl").Directory.Name -join ';'

if(-Not (RunCandleForBundle))
{
    Exit -1
}

if(-Not (RunLightForBundle))
{
    Exit -1
}

if(!(Test-Path $DotnetBundleOutput))
{
    throw "Unable to create the dotnet bundle."
    Exit -1
}

Write-Information "Successfully created dotnet bundle - $DotnetBundleOutput"

exit $LastExitCode
