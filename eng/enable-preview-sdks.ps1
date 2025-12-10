param()

. $PSScriptRoot\common\tools.ps1

try {
    $vsInfo = LocateVisualStudio
}
catch {
    Write-Host "LocateVisualStudio failed: $_"
    return
}

if ($null -eq $vsInfo) {
    Write-Host "No Visual Studio instance detected; preview SDKs remain enabled by default."
    return
}

$vsId = $vsInfo.instanceId
$vsMajorVersion = $vsInfo.installationVersion.Split('.')[0]
$instanceDir = Join-Path $env:USERPROFILE "AppData\Local\Microsoft\VisualStudio\$vsMajorVersion.0_$vsId"

Create-Directory $instanceDir

$sdkFile = Join-Path $instanceDir 'sdk.txt'

$desiredLine = 'UsePreviews=True'
$existingLines = @()

if (Test-Path $sdkFile) {
    $existingLines = @(Get-Content -Path $sdkFile -Encoding ASCII)
}

# Determine how to place the UsePreviews flag based on existing content.
$replacementIndex = -1
for ($i = 0; $i -lt $existingLines.Count; $i++) {
    if ($existingLines[$i] -match '^UsePreviews=.*$') {
        $replacementIndex = $i
        break
    }
}

# Replace the existing line to enforce it as True
if ($replacementIndex -ge 0) {
    $updatedLines = $existingLines
    $updatedLines[$replacementIndex] = $desiredLine
}
elseif ($existingLines.Count -gt 0) {
    # Write to the top of the file but keep the remaining portion (assumption: order does not matter to VS)
    $updatedLines = @($desiredLine) + $existingLines
}
else {
    # Write a whole new file
    $updatedLines = @($desiredLine)
}

Set-Content -Path $sdkFile -Value $updatedLines -Encoding ASCII

Write-Host "Updated $sdkFile"
Get-Content -Path $sdkFile | ForEach-Object { Write-Host "  $_" }
