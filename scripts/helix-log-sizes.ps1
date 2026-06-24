<#
.SYNOPSIS
    Analyzes Helix console log sizes for Azure DevOps builds to compare test output volume.

.DESCRIPTION
    Given one or two AzDO build IDs, this script:
    1. Extracts Helix job IDs from the build's "Queue Tests" task logs
    2. Queries the Helix API for all work items in each job
    3. Uses HTTP HEAD to get the console log size (no full download needed)
    4. Produces a summary report with per-work-item sizes and totals

    When two builds are provided (-BaselineBuildId and -ComparisonBuildId),
    it produces a side-by-side comparison showing size differences.

.PARAMETER BaselineBuildId
    The AzDO build ID to use as the baseline (e.g., a passing main build).

.PARAMETER ComparisonBuildId
    Optional. A second AzDO build ID to compare against the baseline.

.PARAMETER Organization
    AzDO organization. Defaults to 'dnceng-public'.

.PARAMETER Project
    AzDO project. Defaults to 'public'.

.PARAMETER Top
    Number of largest work items to show in the report. Defaults to 50.

.PARAMETER OutputCsv
    Optional path to write full results as CSV.

.EXAMPLE
    # Analyze a single build
    .\scripts\helix-log-sizes.ps1 -BaselineBuildId 1465148

.EXAMPLE
    # Compare two builds
    .\scripts\helix-log-sizes.ps1 -BaselineBuildId 1465148 -ComparisonBuildId 1465200

.EXAMPLE
    # Export full data to CSV
    .\scripts\helix-log-sizes.ps1 -BaselineBuildId 1465148 -OutputCsv results.csv
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$BaselineBuildId,

    [Parameter()]
    [int]$ComparisonBuildId = 0,

    [Parameter()]
    [string]$Organization = 'dnceng-public',

    [Parameter()]
    [string]$Project = 'public',

    [Parameter()]
    [int]$Top = 50,

    [Parameter()]
    [string]$OutputCsv
)

$ErrorActionPreference = 'Stop'

function Get-HelixJobIds {
    param([int]$BuildId, [string]$Org, [string]$Proj)

    Write-Host "  Fetching build timeline for build $BuildId..." -ForegroundColor DarkGray
    $timelineUrl = "https://dev.azure.com/$Org/$Proj/_apis/build/builds/$BuildId/timeline?api-version=7.1"
    $timeline = Invoke-RestMethod -Uri $timelineUrl

    # Find all "Queue Tests" task logs
    $queueTasks = @($timeline.records | Where-Object { $_.name -eq '🟣 Queue Tests' -or $_.name -eq 'Queue Tests' } |
                     Where-Object { $_.type -eq 'Task' -and $_.log })

    if ($queueTasks.Count -eq 0) {
        Write-Warning "No 'Queue Tests' tasks found in build $BuildId"
        return @()
    }

    Write-Host "  Found $($queueTasks.Count) Queue Tests tasks, extracting Helix job IDs..." -ForegroundColor DarkGray

    $jobIds = @()
    foreach ($task in $queueTasks) {
        $logUrl = $task.log.url
        $logContent = Invoke-RestMethod -Uri $logUrl
        $matches = [regex]::Matches($logContent, 'helix\.dot\.net/api/jobs/([0-9a-f-]+)/workitems')
        foreach ($m in $matches) {
            $jobIds += $m.Groups[1].Value
        }
    }

    Write-Host "  Found $($jobIds.Count) Helix jobs" -ForegroundColor DarkGray
    return $jobIds
}

function Get-WorkItemLogSizes {
    param([string[]]$JobIds)

    $results = [System.Collections.Generic.List[PSObject]]::new()
    $totalItems = 0

    foreach ($jobId in $JobIds) {
        Write-Host "  Querying work items for job $jobId..." -ForegroundColor DarkGray
        $url = "https://helix.dot.net/api/jobs/$jobId/workitems?api-version=2019-06-17"
        $workItems = Invoke-RestMethod -Uri $url
        $totalItems += $workItems.Count
        Write-Host "    $($workItems.Count) work items" -ForegroundColor DarkGray

        # Process in batches for progress reporting
        $processed = 0
        foreach ($wi in $workItems) {
            $logSize = 0
            if ($wi.ConsoleOutputUri) {
                try {
                    $resp = Invoke-WebRequest -Uri $wi.ConsoleOutputUri -Method Head -ErrorAction SilentlyContinue
                    $logSize = [long]($resp.Headers['Content-Length'] | Select-Object -First 1)
                }
                catch {
                    # If HEAD fails, try downloading to get size
                    try {
                        $content = Invoke-RestMethod -Uri $wi.ConsoleOutputUri -ErrorAction SilentlyContinue
                        $logSize = [System.Text.Encoding]::UTF8.GetByteCount($content)
                    }
                    catch {
                        $logSize = -1
                    }
                }
            }

            $results.Add([PSCustomObject]@{
                JobId     = $jobId
                WorkItem  = $wi.Name
                ExitCode  = $wi.ExitCode
                State     = $wi.State
                LogSizeBytes = $logSize
                LogSizeKB = [math]::Round($logSize / 1024, 1)
                LogSizeMB = [math]::Round($logSize / (1024 * 1024), 2)
                LogUrl    = $wi.ConsoleOutputUri
            })

            $processed++
            if ($processed % 50 -eq 0) {
                Write-Host "    Processed $processed / $($workItems.Count) work items..." -ForegroundColor DarkGray
            }
        }
    }

    Write-Host "  Total: $totalItems work items processed" -ForegroundColor DarkGray
    return $results
}

function Format-Bytes {
    param([long]$Bytes)
    if ($Bytes -lt 0) { return "N/A" }
    if ($Bytes -ge 1MB) { return "{0:N1} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N1} KB" -f ($Bytes / 1KB) }
    return "$Bytes B"
}

function Show-Report {
    param(
        [PSObject[]]$Data,
        [string]$Label,
        [int]$TopN
    )

    $totalBytes = ($Data | Measure-Object -Property LogSizeBytes -Sum).Sum
    $avgBytes = ($Data | Measure-Object -Property LogSizeBytes -Average).Average
    $maxItem = $Data | Sort-Object LogSizeBytes -Descending | Select-Object -First 1
    $itemsOver1MB = @($Data | Where-Object { $_.LogSizeBytes -ge 1MB }).Count
    $itemsOver100KB = @($Data | Where-Object { $_.LogSizeBytes -ge 100KB }).Count

    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host "  $Label" -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Work items:        $($Data.Count)"
    Write-Host "  Total log size:    $(Format-Bytes $totalBytes)"
    Write-Host "  Average per item:  $(Format-Bytes ([long]$avgBytes))"
    Write-Host "  Largest item:      $(Format-Bytes $maxItem.LogSizeBytes) ($($maxItem.WorkItem))"
    Write-Host "  Items > 1 MB:      $itemsOver1MB"
    Write-Host "  Items > 100 KB:    $itemsOver100KB"
    Write-Host ""

    Write-Host "  Top $TopN largest work items:" -ForegroundColor Yellow
    Write-Host "  $('-' * 76)" -ForegroundColor DarkGray
    Write-Host ("  {0,-55} {1,10} {2,6}" -f "Work Item", "Size", "Exit") -ForegroundColor Yellow
    Write-Host "  $('-' * 76)" -ForegroundColor DarkGray

    $topItems = $Data | Sort-Object LogSizeBytes -Descending | Select-Object -First $TopN
    foreach ($item in $topItems) {
        $color = if ($item.ExitCode -ne 0) { 'Red' } else { 'White' }
        $name = $item.WorkItem
        if ($name.Length -gt 55) { $name = $name.Substring(0, 52) + "..." }
        Write-Host ("  {0,-55} {1,10} {2,6}" -f $name, (Format-Bytes $item.LogSizeBytes), $item.ExitCode) -ForegroundColor $color
    }
}

function Show-Comparison {
    param(
        [PSObject[]]$Baseline,
        [PSObject[]]$Comparison,
        [int]$TopN
    )

    # Build lookup by work item name
    $baselineLookup = @{}
    foreach ($item in $Baseline) {
        $baselineLookup[$item.WorkItem] = $item
    }

    $comparisonLookup = @{}
    foreach ($item in $Comparison) {
        $comparisonLookup[$item.WorkItem] = $item
    }

    # Calculate diffs for matching work items
    $diffs = [System.Collections.Generic.List[PSObject]]::new()
    $allNames = @($baselineLookup.Keys) + @($comparisonLookup.Keys) | Sort-Object -Unique

    foreach ($name in $allNames) {
        $bSize = if ($baselineLookup.ContainsKey($name)) { $baselineLookup[$name].LogSizeBytes } else { 0 }
        $cSize = if ($comparisonLookup.ContainsKey($name)) { $comparisonLookup[$name].LogSizeBytes } else { 0 }
        $diff = $cSize - $bSize
        $pctChange = if ($bSize -gt 0) { [math]::Round(($diff / $bSize) * 100, 1) } else { 0 }

        $diffs.Add([PSCustomObject]@{
            WorkItem       = $name
            BaselineBytes  = $bSize
            ComparisonBytes = $cSize
            DiffBytes      = $diff
            PctChange      = $pctChange
        })
    }

    $totalBaseline = ($Baseline | Measure-Object -Property LogSizeBytes -Sum).Sum
    $totalComparison = ($Comparison | Measure-Object -Property LogSizeBytes -Sum).Sum
    $totalDiff = $totalComparison - $totalBaseline
    $totalPct = if ($totalBaseline -gt 0) { [math]::Round(($totalDiff / $totalBaseline) * 100, 1) } else { 0 }

    Write-Host ""
    Write-Host "=" * 90 -ForegroundColor Magenta
    Write-Host "  COMPARISON SUMMARY" -ForegroundColor Magenta
    Write-Host "=" * 90 -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  Total baseline:    $(Format-Bytes $totalBaseline)"
    Write-Host "  Total comparison:  $(Format-Bytes $totalComparison)"
    Write-Host "  Difference:        $(Format-Bytes ([math]::Abs($totalDiff))) $(if ($totalDiff -lt 0) { 'SMALLER' } else { 'LARGER' }) ($totalPct%)"
    Write-Host ""

    # Show items with biggest reductions
    $reductions = $diffs | Where-Object { $_.DiffBytes -lt 0 } | Sort-Object DiffBytes | Select-Object -First $TopN
    if ($reductions.Count -gt 0) {
        Write-Host "  Top reductions (comparison is smaller):" -ForegroundColor Green
        Write-Host "  $('-' * 86)" -ForegroundColor DarkGray
        Write-Host ("  {0,-45} {1,12} {2,12} {3,12}" -f "Work Item", "Baseline", "New", "Saved") -ForegroundColor Green
        Write-Host "  $('-' * 86)" -ForegroundColor DarkGray
        foreach ($item in $reductions) {
            $name = $item.WorkItem
            if ($name.Length -gt 45) { $name = $name.Substring(0, 42) + "..." }
            Write-Host ("  {0,-45} {1,12} {2,12} {3,12}" -f $name, (Format-Bytes $item.BaselineBytes), (Format-Bytes $item.ComparisonBytes), (Format-Bytes ([math]::Abs($item.DiffBytes)))) -ForegroundColor Green
        }
    }

    Write-Host ""

    # Show items with biggest increases
    $increases = $diffs | Where-Object { $_.DiffBytes -gt 0 } | Sort-Object DiffBytes -Descending | Select-Object -First 10
    if ($increases.Count -gt 0) {
        Write-Host "  Top increases (comparison is larger):" -ForegroundColor Red
        Write-Host "  $('-' * 86)" -ForegroundColor DarkGray
        Write-Host ("  {0,-45} {1,12} {2,12} {3,12}" -f "Work Item", "Baseline", "New", "Added") -ForegroundColor Red
        Write-Host "  $('-' * 86)" -ForegroundColor DarkGray
        foreach ($item in $increases) {
            $name = $item.WorkItem
            if ($name.Length -gt 45) { $name = $name.Substring(0, 42) + "..." }
            Write-Host ("  {0,-45} {1,12} {2,12} {3,12}" -f $name, (Format-Bytes $item.BaselineBytes), (Format-Bytes $item.ComparisonBytes), (Format-Bytes $item.DiffBytes)) -ForegroundColor Red
        }
    }
}

# ── Main ──

Write-Host ""
Write-Host "Helix Log Size Analyzer" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

# Baseline
Write-Host ""
Write-Host "📊 Analyzing baseline build $BaselineBuildId..." -ForegroundColor Yellow
$baselineJobs = Get-HelixJobIds -BuildId $BaselineBuildId -Org $Organization -Proj $Project
if ($baselineJobs.Count -eq 0) {
    Write-Error "No Helix jobs found for baseline build $BaselineBuildId"
    exit 1
}
$baselineData = Get-WorkItemLogSizes -JobIds $baselineJobs
Show-Report -Data $baselineData -Label "BASELINE (Build $BaselineBuildId)" -TopN $Top

# Comparison (optional)
if ($ComparisonBuildId -gt 0) {
    Write-Host ""
    Write-Host "📊 Analyzing comparison build $ComparisonBuildId..." -ForegroundColor Yellow
    $comparisonJobs = Get-HelixJobIds -BuildId $ComparisonBuildId -Org $Organization -Proj $Project
    if ($comparisonJobs.Count -eq 0) {
        Write-Error "No Helix jobs found for comparison build $ComparisonBuildId"
        exit 1
    }
    $comparisonData = Get-WorkItemLogSizes -JobIds $comparisonJobs
    Show-Report -Data $comparisonData -Label "COMPARISON (Build $ComparisonBuildId)" -TopN $Top
    Show-Comparison -Baseline $baselineData -Comparison $comparisonData -TopN $Top
}

# CSV export
if ($OutputCsv) {
    $exportData = $baselineData
    if ($ComparisonBuildId -gt 0) {
        $baselineData | ForEach-Object { $_ | Add-Member -NotePropertyName 'Build' -NotePropertyValue "Baseline-$BaselineBuildId" -PassThru }
        $comparisonData | ForEach-Object { $_ | Add-Member -NotePropertyName 'Build' -NotePropertyValue "Comparison-$ComparisonBuildId" -PassThru }
        $exportData = @($baselineData) + @($comparisonData)
    }
    $exportData | Export-Csv -Path $OutputCsv -NoTypeInformation
    Write-Host ""
    Write-Host "📁 Full data exported to $OutputCsv" -ForegroundColor Green
}

Write-Host ""
