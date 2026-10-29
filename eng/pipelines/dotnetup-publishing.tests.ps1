# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

$ErrorActionPreference = 'Stop'
. $PSScriptRoot/dotnetup-publishing.ps1

$root = Join-Path ([IO.Path]::GetTempPath()) "dotnetup-publishing-tests-$([Guid]::NewGuid())"
$source = Join-Path $root 'source'
$script:signatureStatus = 'Valid'
$script:corruptDownload = $false
$script:assertions = 0

function Assert-Fails {
    param([scriptblock]$Action, [string]$Message)

    $failure = $null
    try { & $Action } catch { $failure = $_ }
    if ($null -eq $failure -or $failure.Exception.Message -notlike "*$Message*") {
        throw "Expected failure containing '$Message'; got '$failure'."
    }
    $script:assertions++
}

# Keep these checks offline and independent of Windows signing infrastructure.
function Get-AuthenticodeSignature {
    param([string]$LiteralPath)
    @{ Status = $script:signatureStatus }
}

function Invoke-WebRequest {
    param($Uri, $OutFile, $MaximumRetryCount, $RetryIntervalSec, $TimeoutSec, $ErrorAction)
    $name = ([Uri]$Uri).Segments[-1]
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $OutFile
    if ($script:corruptDownload) { Add-Content -LiteralPath $OutFile -Value 'corrupted' }
}

try {
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    $names = @(
        'dotnetup-win-x64.exe', 'dotnetup-win-arm64.exe',
        'dotnetup-linux-x64', 'dotnetup-linux-arm64',
        'dotnetup-linux-musl-x64', 'dotnetup-linux-musl-arm64',
        'dotnetup-osx-x64', 'dotnetup-osx-arm64',
        'get-dotnetup.ps1', 'get-dotnetup.sh'
    )
    $bar = @{
        id = 42; azureDevOpsAccount = 'dnceng'; azureDevOpsProject = 'internal'
        azureDevOpsBuildDefinitionId = 1544; azureDevOpsBuildId = 123
        azureDevOpsBranch = 'refs/heads/release/dnup'; commit = ('a' * 40)
        assets = @($names | ForEach-Object {
            $path = Join-Path $source $_
            Set-Content -LiteralPath $path -Value "signed fixture $_"
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash
            Set-Content -LiteralPath "$path.sha512" -Value "$hash  $_"
            @{ name = "dotnetup/0.3.0-preview.1/$_" }
            @{ name = "dotnetup/0.3.0-preview.1/$_.sha512" }
        })
    }
    $build = @{
        id = 123; definition = @{ id = 1544 }; project = @{ name = 'internal' }
        repository = @{ name = 'dotnet-sdk' }; sourceBranch = 'refs/heads/release/dnup'
        sourceVersion = ('a' * 40); status = 'completed'; result = 'succeeded'
        buildNumber = '20261027.2'; templateParameters = @{ runTestBuild = 'False' }
        tags = @('PME Signed', 'BAR ID - 42')
    }

    $release = Get-DotnetupRelease -BARBuildId 42 -BARBuild $bar -SourceBuild $build
    if ($release.files.Count -ne 20 -or $release.version -ne '0.3.0-preview.1') {
        throw 'Incorrect release inventory.'
    }
    $script:assertions++
    $release = $release | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
    $build.templateParameters.runTestBuild = $false
    $null = Get-DotnetupRelease -BARBuildId 42 -BARBuild $bar -SourceBuild $build
    $script:assertions++
    $build.templateParameters.runTestBuild = 'False'

    foreach ($mutation in @(
        { $bad.id = 43 },
        { $bad.azureDevOpsAccount = 'other' },
        { $bad.azureDevOpsProject = 'public' },
        { $bad.azureDevOpsBuildDefinitionId = 1 },
        { $bad.azureDevOpsBranch = 'refs/heads/main' }
    )) {
        $bad = $bar | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
        & $mutation
        Assert-Fails { Get-DotnetupRelease -BARBuildId 42 -BARBuild $bad -SourceBuild $build } 'official pipeline'
    }
    foreach ($mutation in @(
        { $bad.id = 124 },
        { $bad.definition.id = 1 },
        { $bad.project.name = 'public' },
        { $bad.repository.name = 'other' },
        { $bad.sourceBranch = 'refs/heads/main' },
        { $bad.sourceVersion = ('b' * 40) },
        { $bad.status = 'inProgress' },
        { $bad.result = 'partiallySucceeded' },
        { $bad.templateParameters.runTestBuild = 'True' },
        { $bad.templateParameters.Remove('runTestBuild') },
        { $bad.tags = @('BAR ID - 42') },
        { $bad.tags = @('PME Signed', 'BAR ID - 43') }
    )) {
        $bad = $build | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
        & $mutation
        Assert-Fails { Get-DotnetupRelease -BARBuildId 42 -BARBuild $bar -SourceBuild $bad } 'source run'
    }
    foreach ($mutation in @(
        { $bad.assets = $bad.assets[1..19] },
        { $bad.assets[0].name = 'dotnetup/0.3.0-preview.1/unexpected.exe' },
        { $bad.assets[0].name = $bad.assets[1].name },
        { $bad.assets[0].name = 'dotnetup/0.4.0/dotnetup-win-x64.exe' },
        { $bad.assets[0].name = 'dotnetup/../dotnetup-win-x64.exe' }
    )) {
        $bad = $bar | ConvertTo-Json -Depth 10 | ConvertFrom-Json -AsHashtable
        & $mutation
        Assert-Fails { Get-DotnetupRelease -BARBuildId 42 -BARBuild $bad -SourceBuild $build } ''
    }

    $destination = Join-Path $root 'staging'
    $binary = Join-Path $source 'dotnetup-win-x64.exe'
    $original = Get-Content -LiteralPath $binary -Raw
    Add-Content -LiteralPath $binary -Value 'tampered'
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'SHA-512'
    Set-Content -LiteralPath $binary -Value $original -NoNewline
    $script:signatureStatus = 'NotSigned'
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'Authenticode'
    $script:signatureStatus = 'Valid'
    $sidecar = "$binary.sha512"
    $originalChecksum = Get-Content -LiteralPath $sidecar -Raw
    Remove-Item -LiteralPath $sidecar
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } ''
    Set-Content -LiteralPath $sidecar -Value $originalChecksum -NoNewline

    Copy-DotnetupReleaseAssets $release $source $destination
    if (@(Get-ChildItem -LiteralPath $destination -File).Count -ne 20 -or
        @($release.files | Where-Object { $_.sha512 -notmatch '^[0-9a-f]{128}$' }).Count -ne 0) {
        throw 'Staging did not produce the complete hashed inventory.'
    }
    $script:assertions++
    Assert-Fails { Copy-DotnetupReleaseAssets $release $source $destination } 'must not already exist'

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

    $cdnRelease = Get-DotnetupCdnRelease $release $details (Join-Path $root 'verified-download')
    if ($cdnRelease.files.Count -ne 20 -or $cdnRelease.barBuildId -ne 42 -or
        @($cdnRelease.files | Where-Object { -not $_.downloadUrl -or -not $_.sha512 }).Count -ne 0) {
        throw 'Incorrect CDN release output.'
    }
    $script:assertions++
    Write-Host "Passed $script:assertions dotnetup publishing checks."
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
