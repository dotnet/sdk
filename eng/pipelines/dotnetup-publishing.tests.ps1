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
    Copy-Item -LiteralPath (Join-Path $destination "dotnetup/$($release.version)/$name") -Destination $OutFile
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

    $build = @{
        id = 123
        repository = @{ name = 'dotnet-sdk' }; sourceBranch = 'refs/heads/release/dnup'
        sourceVersion = ('a' * 40); status = 'completed'; result = 'succeeded'
        buildNumber = '20261027.2'; templateParameters = @{ runTestBuild = 'False' }
        tags = @('PME Signed')
    }

    $artifacts = [ordered]@{
        'windows_x64' = 'win-x64'; 'windows_arm64' = 'win-arm64'
        'linux_x64_crossBuildLinuxX64' = 'linux-x64'; 'linux_arm64_crossBuildLinuxArm64' = 'linux-arm64'
        'linux_x64_crossBuildMuslX64' = 'linux-musl-x64'; 'linux_arm64_crossBuildMuslArm64' = 'linux-musl-arm64'
        'macOS_x64' = 'osx-x64'; 'macOS_arm64' = 'osx-arm64'
    }
    foreach ($variant in @('preview', 'daily', 'ga')) {
        $version = switch ($variant) {
            preview { '0.3.0-preview.1' }
            daily { '0.3.0-daily.26554.2' }
            ga { '0.3.0' }
        }
        foreach ($artifact in $artifacts.GetEnumerator()) {
            $directory = Join-Path $source "Dotnetup_Dotnetup_$($artifact.Key)/$variant/$version"
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            @{
                schemaVersion = 1; buildId = [string]$build.id; commit = $build.sourceVersion
                variant = $variant; version = $version; rid = $artifact.Value; placeholder = ($variant -eq 'ga')
            } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'release.json')
            $names = @("dotnetup-$($artifact.Value)$(if ($artifact.Value.StartsWith('win-')) { '.exe' })")
            if ($artifact.Value -eq 'win-x64') { $names += 'get-dotnetup.ps1', 'get-dotnetup.sh' }
            foreach ($name in $names) {
                $path = Join-Path $directory $name
                Set-Content -LiteralPath $path -Value "signed $variant fixture $name"
                $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash
                Set-Content -LiteralPath "$path.sha512" -Value $hash
            }
        }
    }

    $release = Get-DotnetupRelease -SourceBuild $build -SourceDirectory $source -Variant preview
    if ($release.files.Count -ne 20 -or $release.version -ne '0.3.0-preview.1' -or
        $release.variant -ne 'preview' -or $release.candidates.Count -ne 8 -or
        $release.azureDevOpsBuildId -ne $build.id -or
        $release.buildNumber -ne $build.buildNumber -or $release.commit -ne $build.sourceVersion) {
        throw 'Incorrect release inventory.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'true'
    $debugOutput = @(Get-DotnetupRelease -SourceBuild $build -SourceDirectory $source -Variant preview 6>&1)
    $messages = @($debugOutput | Where-Object { $_ -is [System.Management.Automation.InformationRecord] })
    if ($messages.Count -ne 3 -or
        [string]$messages[0] -notlike '*runTestBuild=False, PME Signed=True*' -or
        [string]$messages[1] -notlike '*eligibility checks passed*' -or
        [string]$messages[2] -notlike '*0.3.0-preview.1 (preview), 20 files*' -or
        @($debugOutput | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] }).Count -ne 1) {
        throw 'Eligibility and manifest diagnostics must not change the release output.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'false'
    $release = $release | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
    $build.templateParameters.runTestBuild = $false
    $null = Get-DotnetupRelease -SourceBuild $build -SourceDirectory $source -Variant preview
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
        Assert-Fails { Get-DotnetupRelease -SourceBuild $bad -SourceDirectory $source -Variant preview } 'source run'
    }
    Assert-Fails { Get-DotnetupRelease $build $source ga } 'ValidateSet'
    $manifestPath = Join-Path $source 'Dotnetup_Dotnetup_windows_x64/preview/0.3.0-preview.1/release.json'
    $originalManifest = Get-Content -LiteralPath $manifestPath -Raw
    foreach ($mutation in @(
        { $bad.schemaVersion = 2 },
        { $bad.buildId = '124' },
        { $bad.commit = 'b' * 40 },
        { $bad.variant = 'daily' },
        { $bad.placeholder = $true },
        { $bad.placeholder = 'false' },
        { $bad.Remove('placeholder') },
        { $bad.rid = 'win-arm64' },
        { $bad.rid = 'other' },
        { $bad.version = '0.3.0-daily.26554.2' },
        { $bad.version = '../0.3.0-preview.1' },
        { $bad.version = '0.3.0-preview.1/other' }
    )) {
        $bad = $originalManifest | ConvertFrom-Json -AsHashtable
        & $mutation
        $bad | ConvertTo-Json | Set-Content -LiteralPath $manifestPath
        Assert-Fails { Get-DotnetupRelease $build $source preview } ''
        Set-Content -LiteralPath $manifestPath -Value $originalManifest
    }
    Set-Content -LiteralPath $manifestPath -Value '{invalid json'
    Assert-Fails { Get-DotnetupRelease $build $source preview } ''
    Remove-Item -LiteralPath $manifestPath
    Assert-Fails { Get-DotnetupRelease $build $source preview } 'candidate manifests'
    Set-Content -LiteralPath $manifestPath -Value $originalManifest

    $duplicateDirectory = Join-Path $source 'Dotnetup_Dotnetup_duplicate/preview/0.3.0-preview.1'
    New-Item -ItemType Directory -Path $duplicateDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $duplicateDirectory 'release.json') -Value $originalManifest
    Assert-Fails { Get-DotnetupRelease $build $source preview } 'candidate manifests'
    Remove-Item -LiteralPath (Join-Path $source 'Dotnetup_Dotnetup_duplicate') -Recurse -Force

    $otherVersion = Join-Path $source 'Dotnetup_Dotnetup_windows_x64/preview/0.4.0-preview.1'
    Move-Item -LiteralPath (Split-Path $manifestPath) -Destination $otherVersion
    $bad = $originalManifest | ConvertFrom-Json -AsHashtable
    $bad.version = '0.4.0-preview.1'
    $bad | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $otherVersion 'release.json')
    Assert-Fails { Get-DotnetupRelease $build $source preview } 'same version'
    Move-Item -LiteralPath $otherVersion -Destination (Split-Path $manifestPath)
    Set-Content -LiteralPath $manifestPath -Value $originalManifest

    $daily = Get-DotnetupRelease $build $source daily
    $dailyDestination = Join-Path $root 'daily-staging'
    Copy-DotnetupReleaseAssets $daily $source $dailyDestination
    if ($daily.variant -ne 'daily' -or $daily.version -ne '0.3.0-daily.26554.2' -or
        $daily.files.Count -ne 20 -or
        @(Get-ChildItem -LiteralPath $dailyDestination -File -Recurse).Count -ne 20 -or
        @($daily.files | Where-Object { $_.sourceRelativePath -notlike '*/daily/0.3.0-daily.26554.2/*' }).Count -ne 0 -or
        (Get-Content -LiteralPath (Join-Path $dailyDestination "dotnetup/$($daily.version)/dotnetup-win-x64.exe") -Raw) -notlike '*signed daily fixture*') {
        throw 'Daily selection must stage only daily payloads and sidecars.'
    }
    $script:assertions++

    $destination = Join-Path $root 'staging'
    $binary = Join-Path (Split-Path $manifestPath) 'dotnetup-win-x64.exe'
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
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'Checksum does not match'
    if (Test-Path -LiteralPath $destination) { throw 'Checksum failures must not stage any files.' }
    $script:assertions++
    Set-Content -LiteralPath $binary -Value $original -NoNewline
    Set-Content -LiteralPath $sidecar -Value 'invalid checksum'
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'Checksum does not match'
    Set-Content -LiteralPath $sidecar -Value $originalChecksum -NoNewline
    Copy-DotnetupReleaseAssets $release $source $destination
    if (@(Get-ChildItem -LiteralPath $destination -File -Recurse).Count -ne 20 -or
        @(Get-ChildItem -LiteralPath $destination -File).Count -ne 0 -or
        @($release.files | Where-Object {
            $_.relativePath -cne "dotnetup/0.3.0-preview.1/$($_.name)" -or
            $_.sha512 -cne (Get-FileHash -LiteralPath (Join-Path $destination $_.relativePath) -Algorithm SHA512).Hash.ToLowerInvariant()
        }).Count -ne 0) {
        throw 'Staging did not produce the complete hashed inventory under dotnetup/0.3.0-preview.1/.'
    }
    $script:assertions++
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'must not already exist'

    $env:SYSTEM_DEBUG = 'true'
    $debugOutput = @(Copy-DotnetupReleaseAssets $release $source (Join-Path $root 'debug-staging') 6>&1)
    if ($debugOutput.Count -ne 21 -or
        @($debugOutput | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] }).Count -ne 0 -or
        @($debugOutput | Where-Object { [string]$_ -match 'Staged dotnetup/0\.3\.0-preview\.1/[^/]+: \d+ bytes, SHA-512=[0-9a-f]{128}\.' }).Count -ne 20) {
        throw 'Staging diagnostics must include paths and every file size and hash without success-stream output.'
    }
    $script:assertions++
    $env:SYSTEM_DEBUG = 'false'

    $details = @{
        status = 'pass'
        files = @($release.files | ForEach-Object {
            @{ fileDownloadDetails = @(@{ downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/fixture/$($_.relativePath)" }) }
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
    foreach ($invalidPath in @(
        'dotnetup-win-x64.exe',
        'dotnetup/0.4.0/dotnetup-win-x64.exe',
        'notdotnetup/0.3.0-preview.1/dotnetup-win-x64.exe'
    )) {
        $bad = $details | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
        $bad.files[0].fileDownloadDetails[0].downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/fixture/$invalidPath"
        Assert-Fails { Get-DotnetupCdnRelease $release $bad (Join-Path $root 'invalid-layout') } 'must preserve'
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
    if ($cdnRelease.files.Count -ne 20 -or $cdnRelease.azureDevOpsBuildId -ne 123 -or $cdnRelease.variant -ne 'preview' -or
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
