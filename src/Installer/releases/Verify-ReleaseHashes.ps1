<#
.SYNOPSIS
    Downloads and verifies SHA-512 hashes for .NET release archives (.zip and .tar.gz).
.DESCRIPTION
    Fetches the releases.json manifest for a given .NET channel version,
    downloads all .zip and .tar.gz files, computes their SHA-512 hashes,
    and compares against the manifest. Valid files are deleted; mismatched
    files are kept for inspection.

    Designed to run on CI or locally. Exit code 1 if any mismatches found.
.PARAMETER ChannelVersion
    The .NET channel version to verify (e.g., "7.0", "8.0", "9.0", "10.0").
.PARAMETER OutputDir
    Directory to download files into. Defaults to a subdirectory named after the channel.
.PARAMETER Components
    Which component types to verify. Comma-separated list.
    Valid values: sdk, runtime, aspnetcore, windowsdesktop, all
    Defaults to "all".
.PARAMETER ReleaseVersion
    Optional. If specified, only verify files for this specific release version
    (e.g., "7.0.20"). Otherwise verifies all releases in the channel.
.PARAMETER DryRun
    If set, lists files that would be downloaded without actually downloading them.
.EXAMPLE
    .\Verify-ReleaseHashes.ps1 -ChannelVersion 7.0
    .\Verify-ReleaseHashes.ps1 -ChannelVersion 7.0 -Components runtime,aspnetcore
    .\Verify-ReleaseHashes.ps1 -ChannelVersion 7.0 -Components runtime -ReleaseVersion 7.0.20
    .\Verify-ReleaseHashes.ps1 -ChannelVersion 8.0 -DryRun
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ChannelVersion,

    [string]$OutputDir = $null,

    [string]$Components = "all",

    [string]$ReleaseVersion = $null,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if (-not $OutputDir) {
    $OutputDir = Join-Path $PSScriptRoot $ChannelVersion
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Parse component filter
$componentFilter = if ($Components -eq 'all') {
    @('sdk', 'runtime', 'aspnetcore', 'windowsdesktop')
} else {
    $Components -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() }
}

$manifestUrl = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$ChannelVersion/releases.json"
Write-Host "Fetching manifest: $manifestUrl" -ForegroundColor Cyan

try {
    $manifestJson = Invoke-RestMethod -Uri $manifestUrl -UseBasicParsing
}
catch {
    Write-Error "Failed to fetch manifest for $ChannelVersion : $_"
    exit 1
}

# Collect all files to verify: (url, hash, name, releaseVersion, component)
$filesToVerify = [System.Collections.Generic.List[hashtable]]::new()

# Helper function to extract files from a component object
function Add-ComponentFiles {
    param($componentData, $sectionName, $releaseVer, $fileList)
    
    if ($null -eq $componentData) { return }
    
    # Handle both single object and array
    $items = @($componentData)
    
    foreach ($component in $items) {
        if ($null -eq $component -or $null -eq $component.files) { continue }
        foreach ($file in $component.files) {
            $name = $file.name
            if (-not $name) { continue }
            if ($name -notmatch '\.(zip|tar\.gz)$') { continue }
            if (-not $file.url -or -not $file.hash) { continue }
            
            $fileList.Add(@{
                Url          = $file.url
                ExpectedHash = $file.hash
                FileName     = $name
                ReleaseVersion = $releaseVer
                Component    = $sectionName
                Rid          = $file.rid
            })
        }
    }
}

foreach ($release in $manifestJson.releases) {
    $relVer = $release.'release-version'

    # Filter by specific release version if requested
    if ($ReleaseVersion -and $relVer -ne $ReleaseVersion) { continue }

    if ($componentFilter -contains 'sdk') {
        Add-ComponentFiles -componentData $release.sdk -sectionName 'sdk' -releaseVer $relVer -fileList $filesToVerify
        if ($release.sdks) {
            Add-ComponentFiles -componentData $release.sdks -sectionName 'sdks' -releaseVer $relVer -fileList $filesToVerify
        }
    }
    if ($componentFilter -contains 'runtime') {
        Add-ComponentFiles -componentData $release.runtime -sectionName 'runtime' -releaseVer $relVer -fileList $filesToVerify
    }
    if ($componentFilter -contains 'aspnetcore') {
        Add-ComponentFiles -componentData $release.'aspnetcore-runtime' -sectionName 'aspnetcore-runtime' -releaseVer $relVer -fileList $filesToVerify
    }
    if ($componentFilter -contains 'windowsdesktop') {
        Add-ComponentFiles -componentData $release.'windowsdesktop' -sectionName 'windowsdesktop' -releaseVer $relVer -fileList $filesToVerify
    }
}

$totalFiles = $filesToVerify.Count
Write-Host "Found $totalFiles .zip/.tar.gz files to verify across $ChannelVersion releases." -ForegroundColor Green
Write-Host "Components: $($componentFilter -join ', ')" -ForegroundColor Green
if ($ReleaseVersion) { Write-Host "Filtered to release: $ReleaseVersion" -ForegroundColor Green }

if ($DryRun) {
    Write-Host ""
    Write-Host "DRY RUN - Files that would be downloaded:" -ForegroundColor Yellow
    Write-Host "==========================================" -ForegroundColor Yellow
    $grouped = $filesToVerify | Group-Object { $_.ReleaseVersion }
    foreach ($group in $grouped | Sort-Object Name) {
        Write-Host "  Release $($group.Name): $($group.Count) files" -ForegroundColor Cyan
        foreach ($entry in $group.Group) {
            Write-Host "    $($entry.Component)/$($entry.Rid)/$($entry.FileName)" -ForegroundColor White
        }
    }
    Write-Host ""
    Write-Host "Total: $totalFiles files" -ForegroundColor Green
    exit 0
}

# Results tracking (thread-safe)
$allResults = [System.Collections.Concurrent.ConcurrentBag[hashtable]]::new()

# Create a single HttpClient for all downloads (no automatic decompression - critical for hash matching)
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.MaxConnectionsPerServer = 32
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [timespan]::FromMinutes(15)

# Parallel download + verify using runspaces
$maxParallel = 16
$completedCount = [ref]0
$printLock = [object]::new()

$runspacePool = [runspacefactory]::CreateRunspacePool(1, $maxParallel)
$runspacePool.Open()

$jobs = [System.Collections.Generic.List[object]]::new()

$scriptBlock = {
    param($entry, $outputDir, $client, $totalFiles, $completedCount, $printLock, $allResults)

    $url = $entry.Url
    $expectedHash = $entry.ExpectedHash
    $fileName = $entry.FileName
    $releaseVersion = $entry.ReleaseVersion
    $component = $entry.Component
    $rid = $entry.Rid

    $localName = "${releaseVersion}_${component}_${rid}_${fileName}" -replace '[<>:"/\\|?*]', '_'
    $localPath = [System.IO.Path]::Combine($outputDir, $localName)

    $result = @{
        FileName       = $fileName
        ReleaseVersion = $releaseVersion
        Component      = $component
        Rid            = $rid
        Url            = $url
        ExpectedHash   = $expectedHash
        ActualHash     = ''
        Status         = 'Unknown'
        LocalPath      = $localPath
        Error          = ''
    }

    try {
        $response = $client.GetAsync($url).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            $result.Status = 'DownloadFailed'
            $result.Error = "HTTP $($response.StatusCode)"
            $idx = [System.Threading.Interlocked]::Increment($completedCount)
            lock ($printLock) {
                [Console]::ForegroundColor = 'Red'
                [Console]::WriteLine("[$idx/$totalFiles] $url ... DOWNLOAD FAILED ($($response.StatusCode))")
                [Console]::ResetColor()
            }
            $allResults.Add($result)
            $response.Dispose()
            return
        }

        $fs = [System.IO.File]::Create($localPath)
        try {
            $response.Content.CopyToAsync($fs).GetAwaiter().GetResult()
        }
        finally {
            $fs.Close()
            $fs.Dispose()
        }
        $response.Dispose()

        # Compute SHA-512
        $sha512 = [System.Security.Cryptography.SHA512]::Create()
        $fileStream = [System.IO.File]::OpenRead($localPath)
        try {
            $hashBytes = $sha512.ComputeHash($fileStream)
        }
        finally {
            $fileStream.Close()
            $fileStream.Dispose()
            $sha512.Dispose()
        }

        $actualHash = [System.BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
        $result.ActualHash = $actualHash

        if ($actualHash -eq $expectedHash.ToLowerInvariant()) {
            $result.Status = 'Valid'
            [System.IO.File]::Delete($localPath)
            $idx = [System.Threading.Interlocked]::Increment($completedCount)
            lock ($printLock) {
                [Console]::ForegroundColor = 'Green'
                [Console]::WriteLine("[$idx/$totalFiles] $url ... OK")
                [Console]::ResetColor()
            }
        }
        else {
            $result.Status = 'MISMATCH'
            $idx = [System.Threading.Interlocked]::Increment($completedCount)
            lock ($printLock) {
                [Console]::ForegroundColor = 'Red'
                [Console]::WriteLine("[$idx/$totalFiles] $url ... MISMATCH!")
                [Console]::ResetColor()
            }
        }
    }
    catch {
        $result.Status = 'Error'
        $result.Error = $_.Exception.Message
        $idx = [System.Threading.Interlocked]::Increment($completedCount)
        lock ($printLock) {
            [Console]::ForegroundColor = 'Yellow'
            [Console]::WriteLine("[$idx/$totalFiles] $url ... ERROR: $($_.Exception.Message)")
            [Console]::ResetColor()
        }
    }

    $allResults.Add($result)
}

Write-Host "Starting parallel verification with $maxParallel concurrent downloads..." -ForegroundColor Cyan

foreach ($entry in $filesToVerify) {
    $ps = [powershell]::Create()
    $ps.RunspacePool = $runspacePool
    [void]$ps.AddScript($scriptBlock)
    [void]$ps.AddArgument($entry)
    [void]$ps.AddArgument($OutputDir)
    [void]$ps.AddArgument($client)
    [void]$ps.AddArgument($totalFiles)
    [void]$ps.AddArgument($completedCount)
    [void]$ps.AddArgument($printLock)
    [void]$ps.AddArgument($allResults)

    $jobs.Add(@{
        PowerShell = $ps
        Handle     = $ps.BeginInvoke()
    })
}

# Wait for all jobs to complete
foreach ($job in $jobs) {
    $job.PowerShell.EndInvoke($job.Handle)
    $job.PowerShell.Dispose()
}

$runspacePool.Close()
$runspacePool.Dispose()
$client.Dispose()
$handler.Dispose()

# Convert ConcurrentBag to array for downstream processing
$allResults = @($allResults.ToArray())

# Summary
$valid = @($allResults | Where-Object { $_.Status -eq 'Valid' })
$mismatched = @($allResults | Where-Object { $_.Status -eq 'MISMATCH' })
$failed = @($allResults | Where-Object { $_.Status -notin @('Valid', 'MISMATCH') })

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  VERIFICATION RESULTS FOR $ChannelVersion" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Total files checked: $($allResults.Count)" -ForegroundColor White
Write-Host "  Valid (hash match):  $($valid.Count)" -ForegroundColor Green
Write-Host "  MISMATCHED:          $($mismatched.Count)" -ForegroundColor Red
Write-Host "  Failed/Error:        $($failed.Count)" -ForegroundColor Yellow
Write-Host ""

if ($mismatched.Count -gt 0) {
    Write-Host "MISMATCHED FILES (kept for inspection):" -ForegroundColor Red
    Write-Host "----------------------------------------" -ForegroundColor Red
    foreach ($m in $mismatched) {
        Write-Host "  Release:  $($m.ReleaseVersion)" -ForegroundColor White
        Write-Host "  File:     $($m.FileName)" -ForegroundColor White
        Write-Host "  URL:      $($m.Url)" -ForegroundColor White
        Write-Host "  RID:      $($m.Rid)" -ForegroundColor White
        Write-Host "  Component:$($m.Component)" -ForegroundColor White
        Write-Host "  Expected: $($m.ExpectedHash)" -ForegroundColor Yellow
        Write-Host "  Actual:   $($m.ActualHash)" -ForegroundColor Red
        Write-Host "  Kept at:  $($m.LocalPath)" -ForegroundColor Cyan
        Write-Host ""
    }
}

if ($failed.Count -gt 0) {
    Write-Host "FAILED DOWNLOADS:" -ForegroundColor Yellow
    Write-Host "------------------" -ForegroundColor Yellow
    foreach ($f in $failed) {
        Write-Host "  $($f.FileName) ($($f.ReleaseVersion) $($f.Component) $($f.Rid)): $($f.Status) - $($f.Error)" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Export results to CSV for later reference
$csvPath = Join-Path $OutputDir "verification-results-$ChannelVersion.csv"
$allResults | ForEach-Object {
    [PSCustomObject]@{
        ReleaseVersion = $_.ReleaseVersion
        Component      = $_.Component
        Rid            = $_.Rid
        FileName       = $_.FileName
        Status         = $_.Status
        ExpectedHash   = $_.ExpectedHash
        ActualHash     = $_.ActualHash
        Error          = $_.Error
        LocalPath      = $_.LocalPath
        Url            = $_.Url
    }
} | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

Write-Host "Full results exported to: $csvPath" -ForegroundColor Cyan
Write-Host ""

# Exit with code 1 if any mismatches or failures found (useful for CI)
if ($mismatched.Count -gt 0 -or $failed.Count -gt 0) {
    if ($mismatched.Count -gt 0) {
        Write-Host "FAILURE: $($mismatched.Count) hash mismatch(es) detected." -ForegroundColor Red
    }
    if ($failed.Count -gt 0) {
        Write-Host "FAILURE: $($failed.Count) file(s) failed to download or verify." -ForegroundColor Red
    }
    exit 1
}
else {
    Write-Host "SUCCESS: All $($valid.Count) files verified." -ForegroundColor Green
    exit 0
}
