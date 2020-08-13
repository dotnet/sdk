# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$DotnetMSIOutput,
    [Parameter(Mandatory=$true)][string]$WixRoot,
    [Parameter(Mandatory=$true)][string]$ProductMoniker,
    [Parameter(Mandatory=$true)][string]$DotnetMSIVersion,
    [Parameter(Mandatory=$true)][string]$SDKBundleVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLINugetVersion,
    [Parameter(Mandatory=$true)][string]$UpgradeCode,
    [Parameter(Mandatory=$true)][string]$DependencyKeyName,
    [Parameter(Mandatory=$true)][string]$Architecture
)

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
        -dUpgradeCode="$UpgradeCode" `
        -dDependencyKeyName="$DependencyKeyName" `
        -arch "$Architecture" `
        -ext WixDependencyExtension.dll `
        "$PSScriptRoot\sdkplaceholder.wxs" `
        "$PSScriptRoot\provider.wxs"

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
        sdkplaceholder.wixobj `
        provider.wixobj `
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

Write-Information "Creating SdkPlaceholder MSI at $DotnetMSIOutput"

if([string]::IsNullOrEmpty($WixRoot))
{
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
    throw "Unable to create the SdkPlaceholder MSI."
    Exit -1
}

Write-Information "Successfully created SdkPlaceholder MSI - $DotnetMSIOutput"

exit $LastExitCode
