<#
.SYNOPSIS
    Queries and classifies pull requests across .NET SDK repositories for build duty triage.

.DESCRIPTION
    This script queries GitHub for open pull requests from monitored authors across
    dotnet/sdk, dotnet/installer, dotnet/templating, and dotnet/dotnet repositories.
    It classifies each PR into categories (Ready to Merge, Branch Lockdown, Changes Requested,
    Failing/Blocked) and outputs both a human-readable summary and structured JSON.

.PARAMETER IncludeRepo
    Filter to specific repositories. Valid values: sdk, installer, templating, dotnet.
    Default: all four repositories.

.PARAMETER DaysStale
    Number of days after which a PR is flagged as stale. Default: 7.

.PARAMETER OutputJson
    Output only the JSON summary (no human-readable tables).

.PARAMETER SkipCIDetails
    Skip fetching detailed CI status for failing PRs (faster but less detail).

.EXAMPLE
    .\Get-BuildDutyReport.ps1
    # Full report across all repos

.EXAMPLE
    .\Get-BuildDutyReport.ps1 -IncludeRepo sdk,installer
    # Report for sdk and installer only

.EXAMPLE
    .\Get-BuildDutyReport.ps1 -OutputJson
    # JSON-only output for programmatic consumption
#>
[CmdletBinding()]
param(
    [ValidateSet('sdk', 'installer', 'templating', 'dotnet')]
    [string[]]$IncludeRepo = @('sdk', 'installer', 'templating', 'dotnet'),

    [int]$DaysStale = 7,

    [switch]$OutputJson,

    [switch]$SkipCIDetails
)

$ErrorActionPreference = 'Stop'

# Repo full names
$RepoMap = @{
    'sdk'        = 'dotnet/sdk'
    'installer'  = 'dotnet/installer'
    'templating' = 'dotnet/templating'
    'dotnet'     = 'dotnet/dotnet'
}

# Authors to monitor (for non-VMR repos)
$MonitoredAuthors = @(
    'dotnet-maestro[bot]',
    'github-actions[bot]',
    'vseanreesermsft',
    'dotnet-bot'
)

# For dotnet/dotnet VMR, only include PRs whose titles match these repos
$VmrTitleFilters = @(
    'dotnet/sdk',
    'dotnet/templating',
    'dotnet/deployment-tools',
    'dotnet/source-build-reference-packages'
)

# GraphQL query for fetching PR details efficiently
# This fetches all fields we need in a single call per PR
$PrDetailQuery = @'
query($owner: String!, $repo: String!, $number: Int!) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $number) {
      number
      title
      url
      createdAt
      isDraft
      author { login }
      baseRefName
      mergeable
      changedFiles
      mergeStateStatus
      reviewDecision
      labels(first: 20) { nodes { name } }
      commits(last: 1) {
        nodes {
          commit {
            statusCheckRollup {
              state
              contexts(first: 50) {
                nodes {
                  ... on CheckRun {
                    name
                    conclusion
                    status
                  }
                }
              }
            }
          }
        }
      }
      reviews(last: 10) {
        nodes {
          state
          author { login }
        }
      }
    }
  }
}
'@

function Get-PrDetails {
    <#
    .SYNOPSIS
        Fetches detailed PR information via GraphQL.
    #>
    param(
        [string]$Owner,
        [string]$Repo,
        [int]$Number
    )

    $variables = @{
        owner  = $Owner
        repo   = $Repo
        number = $Number
    } | ConvertTo-Json -Compress

    try {
        $result = gh api graphql -f "query=$PrDetailQuery" --field "owner=$Owner" --field "repo=$Repo" --field "number=$Number" 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "GraphQL query failed for $Owner/$Repo#$Number : $result"
            return $null
        }
        return ($result | ConvertFrom-Json).data.repository.pullRequest
    }
    catch {
        Write-Warning "Failed to fetch details for $Owner/$Repo#$Number : $_"
        return $null
    }
}

function Get-OpenPrsByAuthor {
    <#
    .SYNOPSIS
        Lists open PRs by a specific author in a repo using gh CLI.
    #>
    param(
        [string]$Repo,
        [string]$Author
    )

    try {
        $json = gh pr list --repo $Repo --author $Author --state open --json number,title,author --limit 200 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "gh pr list failed for $Repo author:$Author : $json"
            return @()
        }
        $prs = $json | ConvertFrom-Json
        if ($null -eq $prs) { return @() }
        return @($prs)
    }
    catch {
        Write-Warning "Failed to list PRs for $Repo author:$Author : $_"
        return @()
    }
}

function Test-IsMergePr {
    <#
    .SYNOPSIS
        Checks if a PR from github-actions[bot] is an inter-branch merge PR (not a backport).
    #>
    param([string]$Title)

    # Merge PRs have titles like "[automated] Merge branch 'release/9.0.2xx' => 'release/9.0.3xx'"
    return $Title -match '(?i)merge\s+branch|^\[automated\]\s+Merge'
}

function Test-IsVmrSdkPr {
    <#
    .SYNOPSIS
        Checks if a dotnet/dotnet VMR PR is owned by the SDK team based on title.
    #>
    param([string]$Title)

    foreach ($filter in $VmrTitleFilters) {
        if ($Title -match [regex]::Escape($filter)) {
            return $true
        }
    }
    return $false
}

function Get-PrCategory {
    <#
    .SYNOPSIS
        Classifies a PR into a triage category based on its status.
    #>
    param([PSCustomObject]$Pr)

    $labels = @()
    if ($Pr.labels -and $Pr.labels.nodes) {
        $labels = @($Pr.labels.nodes | ForEach-Object { $_.name })
    }

    # Branch Lockdown takes priority
    if ($labels -contains 'Branch Lockdown') {
        return 'lockdown'
    }

    # DO NOT MERGE
    if ($labels -contains 'DO NOT MERGE') {
        return 'blocked'
    }

    # Draft PRs
    if ($Pr.isDraft) {
        return 'draft'
    }

    # Changes Requested
    if ($Pr.reviewDecision -eq 'CHANGES_REQUESTED') {
        return 'changes_requested'
    }

    # Check merge state
    $mergeState = $Pr.mergeStateStatus
    $checkState = $null
    if ($Pr.commits -and $Pr.commits.nodes -and $Pr.commits.nodes.Count -gt 0) {
        $rollup = $Pr.commits.nodes[0].commit.statusCheckRollup
        if ($rollup) {
            $checkState = $rollup.state
        }
    }

    # Ready to merge: CLEAN merge state and SUCCESS checks
    if ($mergeState -eq 'CLEAN' -and $checkState -eq 'SUCCESS') {
        return 'ready'
    }

    # Also ready if merge state is CLEAN (checks may not be required)
    if ($mergeState -eq 'CLEAN') {
        return 'ready'
    }

    # Unstable: non-required checks failing
    if ($mergeState -eq 'UNSTABLE') {
        return 'ready'  # Non-required checks failing is still mergeable
    }

    # Everything else is blocked/failing
    return 'blocked'
}

function Get-AgeDays {
    <#
    .SYNOPSIS
        Computes the age of a PR in days from its creation date.
    #>
    param([string]$CreatedAt)

    $created = [DateTimeOffset]::Parse($CreatedAt)
    $age = [DateTimeOffset]::UtcNow - $created
    return [math]::Max(0, [math]::Floor($age.TotalDays))
}

function Format-AgeString {
    param([int]$Days)
    if ($Days -eq 0) { return "<1d" }
    return "${Days}d"
}

function Get-ChangesRequestedReviewer {
    <#
    .SYNOPSIS
        Gets the reviewer who requested changes.
    #>
    param($Reviews)

    if (-not $Reviews -or -not $Reviews.nodes) { return $null }

    $changesRequested = $Reviews.nodes | Where-Object { $_.state -eq 'CHANGES_REQUESTED' } | Select-Object -Last 1
    if ($changesRequested -and $changesRequested.author) {
        return $changesRequested.author.login
    }
    return $null
}

function Get-CheckRunDetails {
    <#
    .SYNOPSIS
        Extracts per-check-run details (name, conclusion, status) from a PR's commit status.
        Returns a summary of total, passed, failed, and pending checks plus the failed check names.
    #>
    param([PSCustomObject]$Pr)

    $result = [PSCustomObject]@{
        total       = 0
        passed      = 0
        failed      = 0
        pending     = 0
        failedNames = @()
    }

    if (-not $Pr.commits -or -not $Pr.commits.nodes -or $Pr.commits.nodes.Count -eq 0) {
        return $result
    }

    $rollup = $Pr.commits.nodes[0].commit.statusCheckRollup
    if (-not $rollup -or -not $rollup.contexts -or -not $rollup.contexts.nodes) {
        return $result
    }

    # Filter to actual CI checks (exclude Maestro policy checks and license/cla)
    $ciChecks = @($rollup.contexts.nodes | Where-Object {
        $_.name -and
        $_.name -notmatch '^Maestro' -and
        $_.name -notmatch '^license/' -and
        $_.name -notmatch '^WIP$'
    })

    $result.total = $ciChecks.Count
    $result.passed = @($ciChecks | Where-Object { $_.conclusion -eq 'SUCCESS' }).Count
    $result.failed = @($ciChecks | Where-Object { $_.conclusion -eq 'FAILURE' }).Count
    $result.pending = @($ciChecks | Where-Object { $_.status -ne 'COMPLETED' }).Count
    $result.failedNames = @($ciChecks | Where-Object { $_.conclusion -eq 'FAILURE' } | ForEach-Object { $_.name })

    return $result
}

function Test-HasDarcConflictComment {
    <#
    .SYNOPSIS
        Checks if a PR has a comment from maestro indicating a darc merge conflict
        that requires running 'darc vmr resolve-conflict' to fix.
    #>
    param(
        [string]$Repo,
        [int]$Number
    )

    try {
        $comments = gh pr view $Number --repo $Repo --json comments --jq '.comments[-5:][].body' 2>&1
        if ($LASTEXITCODE -ne 0) { return $false }
        foreach ($body in $comments) {
            if ($body -match 'Action Required.*Conflict detected' -and $body -match 'darc vmr resolve-conflict') {
                return $true
            }
        }
        return $false
    }
    catch {
        return $false
    }
}

function Get-PrRecommendation {
    <#
    .SYNOPSIS
        Generates an actionable recommendation for a PR based on its status.
    #>
    param(
        [PSCustomObject]$PrInfo,
        [PSCustomObject]$CheckDetails
    )

    $isMergePr = Test-IsMergePr -Title $PrInfo.title
    $isTemplating = $PrInfo.repo -eq 'dotnet/templating'

    # Merge PR with merge conflicts
    if ($isMergePr -and $PrInfo.mergeable -eq 'CONFLICTING') {
        return 'FIX_MERGE_CONFLICTS'
    }

    # Empty PR (0 changed files, no conflicts) — author-specific guidance
    if ($PrInfo.changedFiles -eq 0 -and $PrInfo.mergeable -ne 'CONFLICTING') {
        # dotnet-bot: inter-branch codeflow with merge commits — merge to reduce churn in the next PR
        if ($PrInfo.author -eq 'dotnet-bot') {
            return 'MERGE_EMPTY_CODEFLOW'
        }

        # dotnet-maestro[bot]: check for darc conflict comment indicating a merge error
        if ($PrInfo.author -eq 'dotnet-maestro[bot]') {
            if (Test-HasDarcConflictComment -Repo $PrInfo.repo -Number $PrInfo.number) {
                return 'FIX_DARC_CONFLICT'
            }
        }

        return 'CLOSE_EMPTY_PR'
    }

    # Templating single-leg failure — likely flaky, rerun
    if ($isTemplating -and $CheckDetails.failed -eq 1 -and $CheckDetails.passed -gt 0) {
        return 'RETRY_SINGLE_LEG'
    }

    # Ready PRs
    if ($PrInfo.category -eq 'ready') {
        return 'MERGE'
    }

    # Lockdown
    if ($PrInfo.category -eq 'lockdown') {
        return 'WAIT_FOR_LOCKDOWN'
    }

    # Changes requested
    if ($PrInfo.category -eq 'changes_requested') {
        return 'ADDRESS_REVIEW'
    }

    # Blocked with all checks passing — likely just needs review
    if ($PrInfo.category -eq 'blocked' -and $PrInfo.checkState -eq 'SUCCESS') {
        return 'NEEDS_REVIEW'
    }

    # Default blocked
    return 'INVESTIGATE_FAILURE'
}

function ConvertTo-PrSummaryObject {
    <#
    .SYNOPSIS
        Converts a PR info object into a summary hashtable for JSON output.
    #>
    param([PSCustomObject]$Pr)
    return [ordered]@{
        repo               = $Pr.repo
        number             = $Pr.number
        title              = $Pr.title
        url                = $Pr.url
        author             = $Pr.author
        targetBranch       = $Pr.targetBranch
        ageDays            = $Pr.ageDays
        mergeable          = $Pr.mergeable
        changedFiles       = $Pr.changedFiles
        mergeStateStatus   = $Pr.mergeStateStatus
        checkState         = $Pr.checkState
        checkDetails       = [ordered]@{
            total       = $Pr.checkDetails.total
            passed      = $Pr.checkDetails.passed
            failed      = $Pr.checkDetails.failed
            pending     = $Pr.checkDetails.pending
            failedNames = $Pr.checkDetails.failedNames
        }
        reviewDecision     = $Pr.reviewDecision
        isStale            = $Pr.isStale
        changesRequestedBy = $Pr.changesRequestedBy
        labels             = $Pr.labels
        recommendation     = $Pr.recommendation
    }
}

# ---- Main ----

Write-Verbose "Build Duty PR Triage Report"
Write-Verbose "Repos: $($IncludeRepo -join ', ')"
Write-Verbose "Stale threshold: $DaysStale days"
Write-Verbose ""

$allPrs = @()

foreach ($repoKey in $IncludeRepo) {
    $repoFull = $RepoMap[$repoKey]
    $owner, $repo = $repoFull -split '/'

    Write-Verbose "Querying $repoFull..."

    if ($repoKey -eq 'dotnet') {
        # VMR: only maestro PRs with SDK-owned titles
        $candidates = Get-OpenPrsByAuthor -Repo $repoFull -Author 'dotnet-maestro[bot]'
        $filtered = @($candidates | Where-Object { Test-IsVmrSdkPr -Title $_.title })
        Write-Verbose "  dotnet-maestro[bot]: $($candidates.Count) total, $($filtered.Count) SDK-owned"
    }
    else {
        $filtered = @()
        foreach ($author in $MonitoredAuthors) {
            $prs = Get-OpenPrsByAuthor -Repo $repoFull -Author $author
            Write-Verbose "  ${author}: $($prs.Count) PRs"

            if ($author -eq 'github-actions[bot]') {
                # Only keep merge PRs, not backports
                $mergePrs = @($prs | Where-Object { Test-IsMergePr -Title $_.title })
                Write-Verbose "    Filtered to $($mergePrs.Count) merge PRs"
                $filtered += $mergePrs
            }
            else {
                $filtered += $prs
            }
        }
    }

    # Fetch details for each PR via GraphQL
    foreach ($pr in $filtered) {
        Write-Verbose "  Fetching details for #$($pr.number)..."
        $details = Get-PrDetails -Owner $owner -Repo $repo -Number $pr.number
        if ($null -eq $details) {
            Write-Warning "  Skipping #$($pr.number) - could not fetch details"
            continue
        }

        $ageDays = Get-AgeDays -CreatedAt $details.createdAt
        $category = Get-PrCategory -Pr $details

        $labels = @()
        if ($details.labels -and $details.labels.nodes) {
            $labels = @($details.labels.nodes | ForEach-Object { $_.name })
        }

        $checkState = $null
        if ($details.commits -and $details.commits.nodes -and $details.commits.nodes.Count -gt 0) {
            $rollup = $details.commits.nodes[0].commit.statusCheckRollup
            if ($rollup) { $checkState = $rollup.state }
        }

        $changesRequestedBy = Get-ChangesRequestedReviewer -Reviews $details.reviews
        $checkDetails = Get-CheckRunDetails -Pr $details

        $prInfo = [PSCustomObject]@{
            repo              = $repoFull
            number            = $details.number
            title             = $details.title
            url               = $details.url
            author            = $details.author.login
            targetBranch      = $details.baseRefName
            createdAt         = $details.createdAt
            ageDays           = $ageDays
            isDraft           = $details.isDraft
            mergeable         = $details.mergeable
            changedFiles      = $details.changedFiles
            mergeStateStatus  = $details.mergeStateStatus
            reviewDecision    = $details.reviewDecision
            checkState        = $checkState
            checkDetails      = $checkDetails
            labels            = $labels
            category          = $category
            isStale           = ($ageDays -ge $DaysStale -and $category -ne 'lockdown')
            changesRequestedBy = $changesRequestedBy
            recommendation    = $null  # filled below
        }

        $prInfo.recommendation = Get-PrRecommendation -PrInfo $prInfo -CheckDetails $checkDetails

        $allPrs += $prInfo
    }
}

# Group by category
$ready = @($allPrs | Where-Object { $_.category -eq 'ready' })
$lockdown = @($allPrs | Where-Object { $_.category -eq 'lockdown' })
$changesRequested = @($allPrs | Where-Object { $_.category -eq 'changes_requested' })
$blocked = @($allPrs | Where-Object { $_.category -eq 'blocked' })
$draft = @($allPrs | Where-Object { $_.category -eq 'draft' })
$stale = @($allPrs | Where-Object { $_.isStale })

# ---- Output ----

if (-not $OutputJson) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Build Duty PR Triage Report" -ForegroundColor Cyan
    Write-Host " Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC' -AsUTC)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Ready to Merge
    Write-Host "✅ Ready to Merge ($($ready.Count))" -ForegroundColor Green
    if ($ready.Count -gt 0) {
        Write-Host ("-" * 120)
        Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5} {5,-10}" -f "Repo", "#", "Title", "Target", "Age", "Checks")
        Write-Host ("-" * 120)
        foreach ($pr in ($ready | Sort-Object repo, ageDays -Descending)) {
            $title = if ($pr.title.Length -gt 57) { $pr.title.Substring(0, 57) + "..." } else { $pr.title }
            $ageStr = Format-AgeString $pr.ageDays
            $staleFlag = if ($pr.isStale) { " ⚠️" } else { "" }
            Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5} {5,-10}" -f $pr.repo, "#$($pr.number)", $title, $pr.targetBranch, "$ageStr$staleFlag", $pr.checkState)
        }
    }
    else {
        Write-Host "  (none)" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Branch Lockdown
    Write-Host "🔒 Branch Lockdown ($($lockdown.Count))" -ForegroundColor Yellow
    if ($lockdown.Count -gt 0) {
        Write-Host ("-" * 120)
        Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5}" -f "Repo", "#", "Title", "Target", "Age")
        Write-Host ("-" * 120)
        foreach ($pr in ($lockdown | Sort-Object repo, ageDays -Descending)) {
            $title = if ($pr.title.Length -gt 57) { $pr.title.Substring(0, 57) + "..." } else { $pr.title }
            $ageStr = Format-AgeString $pr.ageDays
            Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5}" -f $pr.repo, "#$($pr.number)", $title, $pr.targetBranch, $ageStr)
        }
    }
    else {
        Write-Host "  (none)" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Changes Requested
    Write-Host "⚠️  Changes Requested ($($changesRequested.Count))" -ForegroundColor DarkYellow
    if ($changesRequested.Count -gt 0) {
        Write-Host ("-" * 120)
        Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5} {5,-15}" -f "Repo", "#", "Title", "Target", "Age", "Reviewer")
        Write-Host ("-" * 120)
        foreach ($pr in ($changesRequested | Sort-Object repo, ageDays -Descending)) {
            $title = if ($pr.title.Length -gt 57) { $pr.title.Substring(0, 57) + "..." } else { $pr.title }
            $ageStr = Format-AgeString $pr.ageDays
            $reviewer = if ($pr.changesRequestedBy) { "@$($pr.changesRequestedBy)" } else { "unknown" }
            Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5} {5,-15}" -f $pr.repo, "#$($pr.number)", $title, $pr.targetBranch, $ageStr, $reviewer)
        }
    }
    else {
        Write-Host "  (none)" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Failing / Blocked
    Write-Host "❌ Failing / Blocked ($($blocked.Count))" -ForegroundColor Red
    if ($blocked.Count -gt 0) {
        Write-Host ("-" * 140)
        Write-Host ("{0,-18} {1,-6} {2,-50} {3,-25} {4,-5} {5,-10} {6,-25}" -f "Repo", "#", "Title", "Target", "Age", "Checks", "Recommendation")
        Write-Host ("-" * 140)
        foreach ($pr in ($blocked | Sort-Object repo, ageDays -Descending)) {
            $title = if ($pr.title.Length -gt 47) { $pr.title.Substring(0, 47) + "..." } else { $pr.title }
            $ageStr = Format-AgeString $pr.ageDays
            $staleFlag = if ($pr.isStale) { " ⚠️" } else { "" }
            $recStr = switch ($pr.recommendation) {
                'CLOSE_EMPTY_PR'        { '🗑️  Close (empty PR)' }
                'MERGE_EMPTY_CODEFLOW'  { '✅ Merge (empty codeflow)' }
                'FIX_DARC_CONFLICT'     { '🔧 Fix darc conflict' }
                'FIX_MERGE_CONFLICTS'   { '🔀 Fix merge conflicts' }
                'RETRY_SINGLE_LEG'      { "🔄 Retry ($($pr.checkDetails.failedNames -join ', '))" }
                'NEEDS_REVIEW'          { '👀 Needs review approval' }
                'INVESTIGATE_FAILURE'   { '🔍 Investigate' }
                default                 { $pr.recommendation }
            }
            Write-Host ("{0,-18} {1,-6} {2,-50} {3,-25} {4,-5} {5,-10} {6,-25}" -f $pr.repo, "#$($pr.number)", $title, $pr.targetBranch, "$ageStr$staleFlag", $pr.checkState, $recStr)
        }
    }
    else {
        Write-Host "  (none)" -ForegroundColor DarkGray
    }
    Write-Host ""

    # Draft
    if ($draft.Count -gt 0) {
        Write-Host "📝 Draft ($($draft.Count))" -ForegroundColor DarkGray
        Write-Host ("-" * 120)
        Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5}" -f "Repo", "#", "Title", "Target", "Age")
        Write-Host ("-" * 120)
        foreach ($pr in ($draft | Sort-Object repo, ageDays -Descending)) {
            $title = if ($pr.title.Length -gt 57) { $pr.title.Substring(0, 57) + "..." } else { $pr.title }
            $ageStr = Format-AgeString $pr.ageDays
            Write-Host ("{0,-18} {1,-6} {2,-60} {3,-25} {4,-5}" -f $pr.repo, "#$($pr.number)", $title, $pr.targetBranch, $ageStr) -ForegroundColor DarkGray
        }
        Write-Host ""
    }

    # Stale flag
    if ($stale.Count -gt 0) {
        Write-Host "⏳ Stale PRs (>$DaysStale days, excluding Branch Lockdown): $($stale.Count)" -ForegroundColor DarkYellow
        foreach ($pr in $stale) {
            Write-Host "  ⚠️  $($pr.repo)#$($pr.number) - $(Format-AgeString $pr.ageDays) old - $($pr.title)" -ForegroundColor DarkYellow
        }
        Write-Host ""
    }

    # Actionable items
    $emptyPrs = @($allPrs | Where-Object { $_.recommendation -eq 'CLOSE_EMPTY_PR' })
    $emptyCodeflowPrs = @($allPrs | Where-Object { $_.recommendation -eq 'MERGE_EMPTY_CODEFLOW' })
    $darcConflictPrs = @($allPrs | Where-Object { $_.recommendation -eq 'FIX_DARC_CONFLICT' })
    $conflictPrs = @($allPrs | Where-Object { $_.recommendation -eq 'FIX_MERGE_CONFLICTS' })
    $retryPrs = @($allPrs | Where-Object { $_.recommendation -eq 'RETRY_SINGLE_LEG' })
    $reviewPrs = @($allPrs | Where-Object { $_.recommendation -eq 'NEEDS_REVIEW' })

    if ($emptyPrs.Count -gt 0 -or $emptyCodeflowPrs.Count -gt 0 -or $darcConflictPrs.Count -gt 0 -or $conflictPrs.Count -gt 0 -or $retryPrs.Count -gt 0 -or $reviewPrs.Count -gt 0) {
        Write-Host "========================================" -ForegroundColor Magenta
        Write-Host " Quick Actions" -ForegroundColor Magenta
        Write-Host "========================================" -ForegroundColor Magenta

        if ($emptyCodeflowPrs.Count -gt 0) {
            Write-Host ""
            Write-Host "  ✅ Merge empty codeflow PRs (0 file changes, merge commits reduce churn in next PR):" -ForegroundColor Green
            foreach ($pr in $emptyCodeflowPrs) {
                Write-Host "     $($pr.repo)#$($pr.number) - $($pr.title)" -ForegroundColor Green
            }
        }

        if ($emptyPrs.Count -gt 0) {
            Write-Host ""
            Write-Host "  🗑️  Close empty PRs (0 file changes):" -ForegroundColor DarkGray
            foreach ($pr in $emptyPrs) {
                Write-Host "     gh pr close $($pr.number) --repo $($pr.repo) --comment 'Closing: no file changes after merge.'" -ForegroundColor DarkGray
            }
        }

        if ($darcConflictPrs.Count -gt 0) {
            Write-Host ""
            Write-Host "  🔧 Fix darc merge conflicts (run 'darc vmr resolve-conflict'):" -ForegroundColor Yellow
            foreach ($pr in $darcConflictPrs) {
                Write-Host "     $($pr.repo)#$($pr.number) - $($pr.title)" -ForegroundColor Yellow
                Write-Host "     See PR comments for darc resolve-conflict instructions" -ForegroundColor DarkGray
            }
        }

        if ($conflictPrs.Count -gt 0) {
            Write-Host ""
            Write-Host "  🔀 Fix merge conflicts:" -ForegroundColor Yellow
            foreach ($pr in $conflictPrs) {
                Write-Host "     $($pr.repo)#$($pr.number) - $($pr.title)" -ForegroundColor Yellow
            }
        }

        if ($retryPrs.Count -gt 0) {
            Write-Host ""
            Write-Host "  🔄 Retry single-leg failures (likely flaky):" -ForegroundColor Cyan
            foreach ($pr in $retryPrs) {
                $failedLeg = $pr.checkDetails.failedNames -join ', '
                Write-Host "     $($pr.repo)#$($pr.number) - failed: $failedLeg" -ForegroundColor Cyan
                Write-Host "     gh pr comment $($pr.number) --repo $($pr.repo) --body '/azp run'" -ForegroundColor DarkGray
            }
        }

        if ($reviewPrs.Count -gt 0) {
            Write-Host ""
            Write-Host "  👀 Needs review approval (CI passing):" -ForegroundColor Green
            foreach ($pr in $reviewPrs) {
                Write-Host "     $($pr.repo)#$($pr.number) - $($pr.title)" -ForegroundColor Green
            }
        }
        Write-Host ""
    }

    # Summary
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Ready to Merge:     $($ready.Count)" -ForegroundColor Green
    Write-Host "  Branch Lockdown:    $($lockdown.Count)" -ForegroundColor Yellow
    Write-Host "  Changes Requested:  $($changesRequested.Count)" -ForegroundColor DarkYellow
    Write-Host "  Failing/Blocked:    $($blocked.Count)" -ForegroundColor Red
    Write-Host "  Draft:              $($draft.Count)" -ForegroundColor DarkGray
    Write-Host "  Stale (>$($DaysStale)d):       $($stale.Count)" -ForegroundColor DarkYellow
    Write-Host "  Total:              $($allPrs.Count)" -ForegroundColor Cyan
    Write-Host ""
}

# Build and emit JSON summary
$summaryJson = [ordered]@{
    generatedAt        = [DateTimeOffset]::UtcNow.ToString('o')
    repos              = @($IncludeRepo | ForEach-Object { $RepoMap[$_] })
    staleThresholdDays = $DaysStale
    totalPrs           = $allPrs.Count
    counts             = [ordered]@{
        ready            = $ready.Count
        lockdown         = $lockdown.Count
        changesRequested = $changesRequested.Count
        blocked          = $blocked.Count
        draft            = $draft.Count
        stale            = $stale.Count
    }
    prs                = [ordered]@{
        ready            = @($ready | ForEach-Object { ConvertTo-PrSummaryObject $_ })
        lockdown         = @($lockdown | ForEach-Object { ConvertTo-PrSummaryObject $_ })
        changesRequested = @($changesRequested | ForEach-Object { ConvertTo-PrSummaryObject $_ })
        blocked          = @($blocked | ForEach-Object { ConvertTo-PrSummaryObject $_ })
        draft            = @($draft | ForEach-Object { ConvertTo-PrSummaryObject $_ })
    }
    stalePrs           = @($stale | ForEach-Object { ConvertTo-PrSummaryObject $_ })
}

$jsonOutput = $summaryJson | ConvertTo-Json -Depth 10

if ($OutputJson) {
    Write-Output $jsonOutput
}
else {
    Write-Host "[BUILD_DUTY_SUMMARY]"
    Write-Host $jsonOutput
    Write-Host "[/BUILD_DUTY_SUMMARY]"
}
