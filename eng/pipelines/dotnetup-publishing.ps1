# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

function Get-DotnetupRelease {
    param(
        [Parameter(Mandatory)]$SourceBuild,
        [Parameter(Mandatory)][xml]$Manifest
    )

    if ($SourceBuild.repository.name -ne 'dotnet-sdk' -or
        $SourceBuild.sourceBranch -ne 'refs/heads/release/dnup' -or
        $SourceBuild.sourceVersion -notmatch '^[0-9a-f]{40}$' -or
        $SourceBuild.status -ne 'completed' -or
        $SourceBuild.result -ne 'succeeded' -or
        [string]$SourceBuild.templateParameters.runTestBuild -ne 'false' -or
        $SourceBuild.tags -notcontains 'PME Signed') {
        throw 'The source run must be a successful, non-test, PME-signed run of official pipeline 1544 in dnceng/internal on release/dnup.'
    }

    $build = $Manifest.DocumentElement
    if ($build.LocalName -cne 'Build') {
        throw 'The asset manifest must have a Build root element.'
    }

    $fileNames = @(
        'dotnetup-win-x64.exe', 'dotnetup-win-arm64.exe',
        'dotnetup-linux-x64', 'dotnetup-linux-arm64',
        'dotnetup-linux-musl-x64', 'dotnetup-linux-musl-arm64',
        'dotnetup-osx-x64', 'dotnetup-osx-arm64',
        'get-dotnetup.ps1', 'get-dotnetup.sh'
    )
    $expectedNames = @($fileNames | ForEach-Object { $_; "$_.sha512" })
    $assets = @($build.SelectNodes('Blob') | ForEach-Object { $_.GetAttribute('Id') } |
        Where-Object { $_ -like 'dotnetup/*' })
    if ($assets.Count -ne $expectedNames.Count) {
        throw "Expected $($expectedNames.Count) dotnetup assets in the manifest; found $($assets.Count)."
    }

    $versions = @($assets | ForEach-Object {
        if ($_ -notmatch '^dotnetup/([0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)/([^/]+)$') {
            throw "Invalid dotnetup asset path '$_'."
        }
        $Matches[1]
    } | Sort-Object -Unique)
    if ($versions.Count -ne 1) {
        throw 'All dotnetup assets must have the same version.'
    }

    $version = $versions[0]
    foreach ($name in $expectedNames) {
        if (@($assets | Where-Object { $_ -ceq "dotnetup/$version/$name" }).Count -ne 1) {
            throw "Expected exactly one manifest asset named 'dotnetup/$version/$name'."
        }
    }

    [ordered]@{
        azureDevOpsBuildId = $SourceBuild.id
        buildNumber = $SourceBuild.buildNumber
        commit = $SourceBuild.sourceVersion
        version = $version
        files = @($expectedNames | ForEach-Object {
            [ordered]@{ name = $_; relativePath = "dotnetup/$version/$_" }
        })
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

    # BlobArtifacts is flat even though manifest entries carry the versioned publishing path.
    # Validate the complete set before staging anything for ESRP.
    foreach ($file in $Release.files) {
        $path = Join-Path $SourceDirectory $file.name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Missing or empty release asset '$($file.relativePath)'."
        }
    }

    New-Item -ItemType Directory -Path $DestinationDirectory -ErrorAction Stop | Out-Null
    foreach ($file in $Release.files) {
        $path = Join-Path $SourceDirectory $file.name
        $file.sha512 = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash.ToLowerInvariant()
        Copy-Item -LiteralPath $path -Destination (Join-Path $DestinationDirectory $file.name) -ErrorAction Stop
    }
}

function Get-DotnetupCdnRelease {
    param(
        [Parameter(Mandatory)]$Release,
        [Parameter(Mandatory)]$Details,
        [Parameter(Mandatory)][string]$DownloadDirectory
    )

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
        }
    }

    if ($urls.Count -ne $Release.files.Count) {
        throw "Expected $($Release.files.Count) ESRP download URLs; found $($urls.Count)."
    }

    New-Item -ItemType Directory -Path $DownloadDirectory -ErrorAction Stop | Out-Null
    foreach ($file in $Release.files) {
        $path = Join-Path $DownloadDirectory $file.name
        Invoke-WebRequest -Uri $urls[$file.name] -OutFile $path -MaximumRetryCount 3 -RetryIntervalSec 5 -TimeoutSec 300 -ErrorAction Stop
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash -ne $file.sha512) {
            throw "The CDN download does not match the signed build asset '$($file.name)'."
        }
    }

    [ordered]@{
        azureDevOpsBuildId = $Release.azureDevOpsBuildId
        buildNumber = $Release.buildNumber
        commit = $Release.commit
        version = $Release.version
        files = @($Release.files | ForEach-Object {
            [ordered]@{ name = $_.name; sha512 = $_.sha512; downloadUrl = $urls[$_.name] }
        })
    }
}
