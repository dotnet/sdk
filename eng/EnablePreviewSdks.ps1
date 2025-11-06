param()

$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $RepoRoot) {
    throw "Unable to determine repository root."
}

. "$PSScriptRoot\common\tools.ps1"

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
'UsePreviews=True' | Set-Content -Path $sdkFile -Encoding ASCII

Write-Host "Updated $sdkFile"
Get-Content -Path $sdkFile | ForEach-Object { Write-Host "  $_" }
