# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

function Write-DotnetupDebug {
    param([string]$Message)

    if ($env:SYSTEM_DEBUG -eq 'true') {
        Write-Host "##[debug]$Message"
    }
}

function Get-DotnetupRelease {
    param(
        [Parameter(Mandatory)]$SourceBuild,
        [Parameter(Mandatory)][string]$SourceDirectory,
        [Parameter(Mandatory)][ValidateSet('preview', 'daily')][string]$Variant
    )

    Write-DotnetupDebug "Checking build $($SourceBuild.id): branch=$($SourceBuild.sourceBranch), status=$($SourceBuild.status), result=$($SourceBuild.result), runTestBuild=$($SourceBuild.templateParameters.runTestBuild), PME Signed=$($SourceBuild.tags -contains 'PME Signed')."
    if ($SourceBuild.repository.name -ne 'dotnet-sdk' -or
        $SourceBuild.sourceBranch -ne 'refs/heads/release/dnup' -or
        $SourceBuild.sourceVersion -notmatch '^[0-9a-f]{40}$' -or
        $SourceBuild.status -ne 'completed' -or
        $SourceBuild.result -ne 'succeeded' -or
        [string]$SourceBuild.templateParameters.runTestBuild -ne 'false' -or
        $SourceBuild.tags -notcontains 'PME Signed') {
        throw 'The source run must be a successful, non-test, PME-signed run of official pipeline 1544 in dnceng/internal on release/dnup.'
    }
    Write-DotnetupDebug 'Build eligibility checks passed.'

    $rids = @(
        'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64',
        'linux-musl-x64', 'linux-musl-arm64', 'osx-x64', 'osx-arm64'
    )
    $manifests = @(Get-ChildItem -Path (Join-Path $SourceDirectory "Dotnetup_Dotnetup_*/$Variant/*/release.json") -File -ErrorAction Stop)
    if ($manifests.Count -ne $rids.Count) {
        throw "Expected $($rids.Count) $Variant candidate manifests; found $($manifests.Count)."
    }

    $seenRids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $version = $null
    $files = @()
    $candidates = @()
    foreach ($manifest in $manifests) {
        $candidate = Get-Content -LiteralPath $manifest.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($candidate.schemaVersion -ne 1 -or
            [string]$candidate.buildId -cne [string]$SourceBuild.id -or
            $candidate.commit -cne $SourceBuild.sourceVersion -or
            $candidate.variant -cne $Variant -or
            $candidate.placeholder -isnot [bool] -or $candidate.placeholder -ne $false -or
            $rids -cnotcontains $candidate.rid -or -not $seenRids.Add($candidate.rid)) {
            throw "Invalid $Variant candidate provenance, RID, or placeholder flag in '$($manifest.FullName)'."
        }

        if ($candidate.version -cnotmatch "^[0-9]+\.[0-9]+\.[0-9]+-$Variant\.[0-9]+(?:\.[0-9]+)*$" -or
            $manifest.Directory.Name -cne $candidate.version) {
            throw "Invalid $Variant candidate version or directory in '$($manifest.FullName)'."
        }
        if ($null -ne $version -and $candidate.version -cne $version) {
            throw 'All dotnetup candidates must have the same version.'
        }
        $version = $candidate.version
        $sourcePath = [IO.Path]::GetRelativePath($SourceDirectory, $manifest.Directory.FullName).Replace('\', '/')
        $names = @("dotnetup-$($candidate.rid)$(if ($candidate.rid.StartsWith('win-')) { '.exe' })")
        if ($candidate.rid -ceq 'win-x64') {
            $names += 'get-dotnetup.ps1', 'get-dotnetup.sh'
        }
        foreach ($name in $names) {
            foreach ($fileName in @($name, "$name.sha512")) {
                $files += [ordered]@{
                    name = $fileName
                    relativePath = "dotnetup/$version/$fileName"
                    sourceRelativePath = "$sourcePath/$fileName"
                }
            }
        }
        $candidates += [ordered]@{
            rid = $candidate.rid
            sourceManifest = "$sourcePath/release.json"
        }
    }
    Write-DotnetupDebug "Candidates validated: dotnetup $version ($Variant), $($files.Count) files."

    [ordered]@{
        azureDevOpsBuildId = $SourceBuild.id
        buildNumber = $SourceBuild.buildNumber
        commit = $SourceBuild.sourceVersion
        variant = $Variant
        version = $version
        candidates = $candidates
        files = $files
    }
}

function Copy-DotnetupReleaseAssets {
    param(
        [Parameter(Mandatory)]$Release,
        [Parameter(Mandatory)][string]$SourceDirectory,
        [Parameter(Mandatory)][string]$DestinationDirectory
    )

    if (Test-Path -LiteralPath $DestinationDirectory) {
        throw "The staging directory must not already exist: '$DestinationDirectory'."
    }
    Write-DotnetupDebug "Staging $($Release.files.Count) files from '$SourceDirectory' to '$DestinationDirectory'."

    # Validate the complete set before staging anything for ESRP.
    foreach ($file in $Release.files) {
        $path = Join-Path $SourceDirectory $file.sourceRelativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Missing or empty release asset '$($file.relativePath)'."
        }
        $file.sha512 = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash.ToLowerInvariant()
    }
    foreach ($file in $Release.files | Where-Object { -not $_.name.EndsWith('.sha512') }) {
        $checksum = (Get-Content -LiteralPath (Join-Path $SourceDirectory "$($file.sourceRelativePath).sha512") -Raw).Trim()
        if ($checksum -notmatch '^[0-9a-fA-F]{128}$' -or $checksum -ne $file.sha512) {
            throw "Checksum does not match release asset '$($file.relativePath)'."
        }
    }

    New-Item -ItemType Directory -Path (Join-Path $DestinationDirectory "dotnetup/$($Release.version)") -Force -ErrorAction Stop | Out-Null
    foreach ($file in $Release.files) {
        $path = Join-Path $SourceDirectory $file.sourceRelativePath
        Copy-Item -LiteralPath $path -Destination (Join-Path $DestinationDirectory $file.relativePath) -ErrorAction Stop
        Write-DotnetupDebug "Staged $($file.relativePath): $((Get-Item -LiteralPath $path).Length) bytes, SHA-512=$($file.sha512)."
    }
}

function Get-DotnetupCdnRelease {
    param(
        [Parameter(Mandatory)]$Release,
        [Parameter(Mandatory)]$Details,
        [Parameter(Mandatory)][string]$DownloadDirectory
    )

    Write-DotnetupDebug "Checking ESRP result: status=$($Details.status), expected files=$($Release.files.Count)."
    if ($Details.status -ne 'pass') {
        throw "ESRP publishing did not pass. Status: '$($Details.status)'."
    }

    $urls = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($file in $Details.files) {
        foreach ($download in $file.fileDownloadDetails) {
            $uri = $null
            if (-not [Uri]::TryCreate($download.downloadUrl, [UriKind]::Absolute, [ref]$uri) -or
                $uri.Scheme -ne 'https' -or
                $uri.Host -ne 'download.visualstudio.microsoft.com' -or
                -not $uri.IsDefaultPort -or
                $uri.UserInfo -ne '' -or $uri.Query -ne '' -or $uri.Fragment -ne '') {
                throw "Unexpected ESRP download URL '$($download.downloadUrl)'."
            }
            $name = [Uri]::UnescapeDataString($uri.Segments[-1])
            if ($Release.files.name -cnotcontains $name -or -not $urls.TryAdd($name, $uri.AbsoluteUri)) {
                throw "Unexpected or duplicate ESRP download '$name'."
            }
            $asset = $Release.files | Where-Object { $_.name -ceq $name }
            if (-not [Uri]::UnescapeDataString($uri.AbsolutePath).EndsWith("/$($asset.relativePath)", [StringComparison]::Ordinal)) {
                throw "The ESRP download URL must preserve '$($asset.relativePath)': '$($uri.AbsoluteUri)'."
            }
        }
    }

    if ($urls.Count -ne $Release.files.Count) {
        throw "Expected $($Release.files.Count) ESRP download URLs; found $($urls.Count)."
    }

    New-Item -ItemType Directory -Path $DownloadDirectory -ErrorAction Stop | Out-Null
    foreach ($file in $Release.files) {
        $path = Join-Path $DownloadDirectory $file.name
        Write-DotnetupDebug "Downloading $($file.name) from '$($urls[$file.name])'."
        Invoke-WebRequest -Uri $urls[$file.name] -OutFile $path -MaximumRetryCount 3 -RetryIntervalSec 5 -TimeoutSec 300 -ErrorAction Stop
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash -ne $file.sha512) {
            throw "The CDN download does not match the signed build asset '$($file.name)'."
        }
        Write-DotnetupDebug "Verified CDN file $($file.name): SHA-512=$($file.sha512)."
    }

    [ordered]@{
        azureDevOpsBuildId = $Release.azureDevOpsBuildId
        buildNumber = $Release.buildNumber
        commit = $Release.commit
        variant = $Release.variant
        version = $Release.version
        files = @($Release.files | ForEach-Object {
            [ordered]@{ name = $_.name; sha512 = $_.sha512; downloadUrl = $urls[$_.name] }
        })
    }
}
