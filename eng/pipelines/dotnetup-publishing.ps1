# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

function Get-DotnetupRelease {
    param(
        [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$BARBuildId,
        [Parameter(Mandatory)]$BARBuild,
        [Parameter(Mandatory)]$SourceBuild
    )

    if ($BARBuild.id -ne $BARBuildId -or
        $BARBuild.azureDevOpsAccount -ne 'dnceng' -or
        $BARBuild.azureDevOpsProject -ne 'internal' -or
        $BARBuild.azureDevOpsBuildDefinitionId -ne 1544 -or
        $BARBuild.azureDevOpsBranch -ne 'refs/heads/release/dnup') {
        throw 'The BAR build must identify official pipeline 1544 in dnceng/internal on release/dnup.'
    }

    if ($SourceBuild.id -ne $BARBuild.azureDevOpsBuildId -or
        $SourceBuild.definition.id -ne 1544 -or
        $SourceBuild.project.name -ne 'internal' -or
        $SourceBuild.repository.name -ne 'dotnet-sdk' -or
        $SourceBuild.sourceBranch -ne 'refs/heads/release/dnup' -or
        $SourceBuild.sourceVersion -notmatch '^[0-9a-f]{40}$' -or
        $SourceBuild.sourceVersion -ne $BARBuild.commit -or
        $SourceBuild.status -ne 'completed' -or
        $SourceBuild.result -ne 'succeeded' -or
        [string]$SourceBuild.templateParameters.runTestBuild -ne 'false' -or
        $SourceBuild.tags -notcontains 'PME Signed' -or
        $SourceBuild.tags -notcontains "BAR ID - $BARBuildId") {
        throw 'The source run must be successful, non-test, PME-signed, and match the BAR build and commit.'
    }

    $fileNames = @(
        'dotnetup-win-x64.exe', 'dotnetup-win-arm64.exe',
        'dotnetup-linux-x64', 'dotnetup-linux-arm64',
        'dotnetup-linux-musl-x64', 'dotnetup-linux-musl-arm64',
        'dotnetup-osx-x64', 'dotnetup-osx-arm64',
        'get-dotnetup.ps1', 'get-dotnetup.sh'
    )
    $expectedNames = @($fileNames | ForEach-Object { $_; "$_.sha512" })
    $assets = @($BARBuild.assets | Where-Object { $_.name -like 'dotnetup/*' })
    if ($assets.Count -ne $expectedNames.Count) {
        throw "Expected $($expectedNames.Count) dotnetup assets in BAR; found $($assets.Count)."
    }

    $versions = @($assets | ForEach-Object {
        if ($_.name -notmatch '^dotnetup/([0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)/([^/]+)$') {
            throw "Invalid dotnetup asset path '$($_.name)'."
        }
        $Matches[1]
    } | Sort-Object -Unique)
    if ($versions.Count -ne 1) {
        throw 'All dotnetup assets must have the same version.'
    }

    $version = $versions[0]
    foreach ($name in $expectedNames) {
        if (@($assets | Where-Object { $_.name -ceq "dotnetup/$version/$name" }).Count -ne 1) {
            throw "Expected exactly one BAR asset named 'dotnetup/$version/$name'."
        }
    }

    [ordered]@{
        barBuildId = $BARBuildId
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

    # BlobArtifacts is flat even though BAR names carry the versioned publishing path.
    # Validate the complete set before staging anything for ESRP.
    foreach ($file in $Release.files) {
        $path = Join-Path $SourceDirectory $file.name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Missing or empty release asset '$($file.relativePath)'."
        }
        if ($file.name.EndsWith('.sha512')) {
            continue
        }

        $expectedHash = ((Get-Content -LiteralPath "$path.sha512" -Raw).Trim() -split '\s+')[0]
        if ($expectedHash -notmatch '^[0-9a-fA-F]{128}$' -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash -ne $expectedHash) {
            throw "SHA-512 verification failed for '$($file.name)'."
        }

        if ($file.name.EndsWith('.exe') -or $file.name.EndsWith('.ps1')) {
            $signature = Get-AuthenticodeSignature -LiteralPath $path
            if ($signature.Status -ne 'Valid') {
                throw "Invalid Authenticode signature on '$($file.name)': $($signature.Status)."
            }
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
        barBuildId = $Release.barBuildId
        azureDevOpsBuildId = $Release.azureDevOpsBuildId
        buildNumber = $Release.buildNumber
        commit = $Release.commit
        version = $Release.version
        files = @($Release.files | ForEach-Object {
            [ordered]@{ name = $_.name; sha512 = $_.sha512; downloadUrl = $urls[$_.name] }
        })
    }
}
