# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$inputDir,
    [Parameter(Mandatory=$true)][string]$DotnetMSIOutput,
    [Parameter(Mandatory=$true)][string]$WixRoot,
    [Parameter(Mandatory=$true)][string]$ProductMoniker,
    [Parameter(Mandatory=$true)][string]$DotnetMSIVersion,
    [Parameter(Mandatory=$true)][string]$SDKBundleVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLINugetVersion,
    [Parameter(Mandatory=$true)][string]$VersionMajor,
    [Parameter(Mandatory=$true)][string]$VersionMinor,
    [Parameter(Mandatory=$true)][string]$UpgradeCode,
    [Parameter(Mandatory=$true)][string]$DependencyKeyName,
    [Parameter(Mandatory=$true)][string]$Architecture
)

$InstallFileswsx = ".\template-install-files.wxs"
$InstallFilesWixobj = "template-install-files.wixobj"

function RunHeat
{
    $result = $true
    pushd "$WixRoot"

    Write-Information "Running heat.."

    $heatOutput = .\heat.exe dir `"$inputDir`" -template fragment  `
        -sreg -ag `
        -var var.DotnetSrc  `
        -cg InstallFiles  `
        -srd  `
        -dr DOTNETHOME  `
        -out template-install-files.wxs

    Write-Information "Heat output: $heatOutput"

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Information "Heat failed with exit code $LastExitCode."
    }

    popd
    Write-Information "RunHeat result: $result"
    return $result
}

function RunCandle
{
    $result = $true
    pushd "$WixRoot"

    Write-Information "Running candle.."

    $candleOutput = .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$PSScriptRoot\dummyeula.rtf" `
        -dProductMoniker="$ProductMoniker" `
        -dBuildVersion="$DotnetMSIVersion" `
        -dSDKBundleVersion="$SDKBundleVersion" `
        -dNugetVersion="$DotnetCLINugetVersion" `
        -dVersionMajor="$VersionMajor" `
        -dVersionMinor="$VersionMinor" `
        -dUpgradeCode="$UpgradeCode" `
        -dDependencyKeyName="$DependencyKeyName" `
        -arch "$Architecture" `
        -ext WixDependencyExtension.dll `
        "$PSScriptRoot\dotnethome_x64.wxs" `
        "$PSScriptRoot\templates.wxs" `
        "$PSScriptRoot\provider.wxs" `
        $InstallFileswsx

    Write-Information "Candle output: $candleOutput"

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Information "Candle failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunLight
{
    $result = $true
    pushd "$WixRoot"

    Write-Information "Running light.."
    $CabCache = Join-Path $WixRoot "cabcache"

    $lightOutput = .\light.exe -nologo -ext WixUIExtension -ext WixDependencyExtension -ext WixUtilExtension `
        -cultures:en-us `
        dotnethome_x64.wixobj `
        templates.wixobj `
        provider.wixobj `
        $InstallFilesWixobj `
        -b "$inputDir" `
        -b "$PSScriptRoot" `
        -reusecab `
        -cc "$CabCache" `
        -out $DotnetMSIOutput

    Write-Information "Light output: $lightOutput"

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Information "Light failed with exit code $LastExitCode."
    }

    popd
    return $result
}

if(!(Test-Path $inputDir))
{
    throw "$inputDir not found"
}

Write-Information "Creating templates MSI at $DotnetMSIOutput"

if([string]::IsNullOrEmpty($WixRoot))
{
    Exit -1
}

if(-Not (RunHeat))
{
    Write-Information "Heat failed"
    Exit -1
}

if(-Not (RunCandle))
{
    Write-Information "Candle failed"
    Exit -1
}

if(-Not (RunLight))
{
    Write-Information "Light failed"
    Exit -1
}

if(!(Test-Path $DotnetMSIOutput))
{
    throw "Unable to create the templates MSI."
    Exit -1
}

Write-Information "Successfully created templates MSI - $DotnetMSIOutput"

exit $LastExitCode
