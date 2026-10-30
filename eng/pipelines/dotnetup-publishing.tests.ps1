# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

$ErrorActionPreference = 'Stop'
. $PSScriptRoot/dotnetup-publishing.ps1

$root = Join-Path ([IO.Path]::GetTempPath()) "dotnetup-publishing-tests-$([Guid]::NewGuid())"
$source = Join-Path $root 'source'
$script:corruptDownload = $false
$script:assertions = 0
$originalSystemDebug = $env:SYSTEM_DEBUG

function Assert-Fails {
    param([scriptblock]$Action, [string]$Message)

    $failure = $null
    try { & $Action } catch { $failure = $_ }
    if ($null -eq $failure -or $failure.Exception.Message -notlike "*$Message*") {
        throw "Expected failure containing '$Message'; got '$failure'."
    }
    $script:assertions++
}

function Invoke-WebRequest {
    param($Uri, $OutFile, $MaximumRetryCount, $RetryIntervalSec, $TimeoutSec, $ErrorAction)
    $name = ([Uri]$Uri).Segments[-1]
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $OutFile
    if ($script:corruptDownload) { Add-Content -LiteralPath $OutFile -Value 'corrupted' }
}

try {
    foreach ($debugValue in @('', 'false')) {
        $env:SYSTEM_DEBUG = $debugValue
        if (@(Write-DotnetupDebug 'hidden' 6>&1).Count -ne 0) {
            throw 'Debug logging must be silent unless System.Debug is true.'
        }
        $script:assertions++
    }
    $env:SYSTEM_DEBUG = 'TrUe'
    $debugOutput = @(Write-DotnetupDebug 'enabled' 6>&1)
    if ($debugOutput.Count -ne 1 -or $debugOutput[0] -isnot [System.Management.Automation.InformationRecord] -or
        [string]$debugOutput[0] -cne '##[debug]enabled') {
        throw 'Debug logging must use the Azure debug marker on the information stream.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'false'

    New-Item -ItemType Directory -Path $source -Force | Out-Null
    $names = @(
        'dotnetup-win-x64.exe', 'dotnetup-win-arm64.exe',
        'dotnetup-linux-x64', 'dotnetup-linux-arm64',
        'dotnetup-linux-musl-x64', 'dotnetup-linux-musl-arm64',
        'dotnetup-osx-x64', 'dotnetup-osx-arm64',
        'get-dotnetup.ps1', 'get-dotnetup.sh'
    )
    [xml]$manifest = @"
<Build>
  <Blob Id="assets/manifests/dotnet-sdk/20261027.2/MergedManifest.xml" NonShipping="True" />
</Build>
"@
    foreach ($name in $names) {
        $path = Join-Path $source $name
        Set-Content -LiteralPath $path -Value "signed fixture $name"
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash
        Set-Content -LiteralPath "$path.sha512" -Value "$hash  $name"
        foreach ($fileName in @($name, "$name.sha512")) {
            $blob = $manifest.CreateElement('Blob')
            $blob.SetAttribute('Id', "dotnetup/0.3.0-preview.1/$fileName")
            [void]$manifest.DocumentElement.AppendChild($blob)
        }
    }
    $manifest.Save((Join-Path $source 'MergedManifest.xml'))
    $build = @{
        id = 123
        repository = @{ name = 'dotnet-sdk' }; sourceBranch = 'refs/heads/release/dnup'
        sourceVersion = ('a' * 40); status = 'completed'; result = 'succeeded'
        buildNumber = '20261027.2'; templateParameters = @{ runTestBuild = 'False' }
        tags = @('PME Signed')
    }

    $release = Get-DotnetupRelease -SourceBuild $build -Manifest $manifest
    if ($release.files.Count -ne 20 -or $release.version -ne '0.3.0-preview.1' -or
        $release.azureDevOpsBuildId -ne $build.id -or
        $release.buildNumber -ne $build.buildNumber -or $release.commit -ne $build.sourceVersion) {
        throw 'Incorrect release inventory.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'true'
    $debugOutput = @(Get-DotnetupRelease -SourceBuild $build -Manifest $manifest 6>&1)
    $messages = @($debugOutput | Where-Object { $_ -is [System.Management.Automation.InformationRecord] })
    if ($messages.Count -ne 3 -or
        [string]$messages[0] -notlike '*runTestBuild=False, PME Signed=True*' -or
        [string]$messages[1] -notlike '*eligibility checks passed*' -or
        [string]$messages[2] -notlike '*0.3.0-preview.1, 20 files*' -or
        @($debugOutput | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] }).Count -ne 1) {
        throw 'Eligibility and manifest diagnostics must not change the release output.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'false'
    $release = $release | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
    $build.templateParameters.runTestBuild = $false
    $null = Get-DotnetupRelease -SourceBuild $build -Manifest $manifest
    $script:assertions++
    $build.templateParameters.runTestBuild = 'False'

    foreach ($mutation in @(
        { $bad.repository.name = 'other' },
        { $bad.sourceBranch = 'refs/heads/main' },
        { $bad.sourceVersion = 'invalid' },
        { $bad.status = 'inProgress' },
        { $bad.result = 'partiallySucceeded' },
        { $bad.templateParameters.runTestBuild = 'True' },
        { $bad.templateParameters.Remove('runTestBuild') },
        { $bad.tags = @() }
    )) {
        $bad = $build | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
        & $mutation
        Assert-Fails { Get-DotnetupRelease -SourceBuild $bad -Manifest $manifest } 'source run'
    }
    Assert-Fails { Get-DotnetupRelease -SourceBuild $build -Manifest ([xml]'<Other />') } 'Build root element'
    foreach ($mutation in @(
        { [void]$bad.DocumentElement.RemoveChild($bad.Build.Blob[1]) },
        { $bad.Build.Blob[1].SetAttribute('Id', 'dotnetup/0.3.0-preview.1/unexpected.exe') },
        { $bad.Build.Blob[1].SetAttribute('Id', $bad.Build.Blob[2].Id) },
        { $bad.Build.Blob[1].SetAttribute('Id', 'dotnetup/0.4.0/dotnetup-win-x64.exe') },
        { $bad.Build.Blob[1].SetAttribute('Id', 'dotnetup/../dotnetup-win-x64.exe') }
    )) {
        $bad = [xml]$manifest.OuterXml
        & $mutation
        Assert-Fails { Get-DotnetupRelease -SourceBuild $build -Manifest $bad } ''
    }

    $destination = Join-Path $root 'staging'
    $binary = Join-Path $source 'dotnetup-win-x64.exe'
    $original = Get-Content -LiteralPath $binary -Raw
    Set-Content -LiteralPath $binary -Value '' -NoNewline
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'Missing or empty release asset'
    Set-Content -LiteralPath $binary -Value $original -NoNewline
    $sidecar = "$binary.sha512"
    $originalChecksum = Get-Content -LiteralPath $sidecar -Raw
    Remove-Item -LiteralPath $sidecar
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'Missing or empty release asset'
    Set-Content -LiteralPath $sidecar -Value $originalChecksum -NoNewline

    Add-Content -LiteralPath $binary -Value 'fixture content without a matching sidecar'
    Copy-DotnetupReleaseAssets $release $source $destination
    if (@(Get-ChildItem -LiteralPath $destination -File).Count -ne 20 -or
        @($release.files | Where-Object {
            $_.sha512 -cne (Get-FileHash -LiteralPath (Join-Path $destination $_.name) -Algorithm SHA512).Hash.ToLowerInvariant()
        }).Count -ne 0) {
        throw 'Staging did not produce the complete hashed inventory.'
    }
    $script:assertions++
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'must not already exist'

    $env:SYSTEM_DEBUG = 'true'
    $debugOutput = @(Copy-DotnetupReleaseAssets $release $source (Join-Path $root 'debug-staging') 6>&1)
    if ($debugOutput.Count -ne 21 -or
        @($debugOutput | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] }).Count -ne 0 -or
        @($debugOutput | Where-Object { [string]$_ -match 'Staged .+: \d+ bytes, SHA-512=[0-9a-f]{128}\.' }).Count -ne 20) {
        throw 'Staging diagnostics must include paths and every file size and hash without success-stream output.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'false'

    $details = @{
        status = 'pass'
        files = @($release.files | ForEach-Object {
            @{ fileDownloadDetails = @(@{ downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/fixture/$($_.name)" }) }
        })
    }
    foreach ($mutation in @(
        { $bad.status = 'failed' },
        { $bad.files = @() },
        { $bad.files = $bad.files[1..19] },
        { $bad.files[0] = $bad.files[1] },
        { $bad.files[0].fileDownloadDetails[0].downloadUrl = 'http://download.visualstudio.microsoft.com/dotnetup-win-x64.exe' },
        { $bad.files[0].fileDownloadDetails[0].downloadUrl = 'https://example.com/dotnetup-win-x64.exe' },
        { $bad.files[0].fileDownloadDetails[0].downloadUrl += '?token=unexpected' },
        { $bad.files[0].fileDownloadDetails[0].downloadUrl += '.unexpected' }
    )) {
        $bad = $details | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
        & $mutation
        Assert-Fails { Get-DotnetupCdnRelease $release $bad (Join-Path $root 'invalid-download') } ''
    }
    $script:corruptDownload = $true
    Assert-Fails { Get-DotnetupCdnRelease $release $details (Join-Path $root 'corrupt-download') } 'does not match'
    $script:corruptDownload = $false

    # The release job imports its helper and metadata from artifact inputs, not a checkout.
    $tools = Join-Path $root 'release-tools'
    New-Item -ItemType Directory -Path $tools | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dotnetup-publishing.ps1') -Destination $tools
    . (Join-Path $tools 'dotnetup-publishing.ps1')
    $releasePath = Join-Path $root 'release.json'
    $release | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $releasePath
    $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json -AsHashtable

    $env:SYSTEM_DEBUG = 'true'
    $debugOutput = @(Get-DotnetupCdnRelease $release $details (Join-Path $root 'verified-download') 6>&1)
    $cdnRelease = @($debugOutput | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] })
    if ($cdnRelease.Count -ne 1 -or
        @($debugOutput | Where-Object { ([string]$_).StartsWith('##[debug]Downloading ') }).Count -ne 20 -or
        @($debugOutput | Where-Object { ([string]$_).StartsWith('##[debug]Verified CDN file ') }).Count -ne 20) {
        throw 'CDN diagnostics must report every download and verified hash without changing the release output.'
    }
    $script:assertions++
    $cdnRelease = $cdnRelease[0]
    if ($cdnRelease.files.Count -ne 20 -or $cdnRelease.azureDevOpsBuildId -ne 123 -or
        @($cdnRelease.files | Where-Object { -not $_.downloadUrl -or -not $_.sha512 }).Count -ne 0) {
        throw 'Incorrect CDN release output.'
    }
    $script:assertions++
    Write-Host "Passed $script:assertions dotnetup publishing checks."
}
finally {
    $env:SYSTEM_DEBUG = $originalSystemDebug
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
