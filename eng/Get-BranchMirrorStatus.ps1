#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check if public GitHub release branches have been mirrored to internal Azure DevOps repos.

.DESCRIPTION
    For each configured branch mapping (dotnet/sdk, dotnet/installer), fetches the latest
    commit on the public GitHub branch and checks whether it has been mirrored to the
    corresponding internal/release/* branch in Azure DevOps (dnceng org, internal project).

    Output is always plain markdown, safe for piping to a file or rendering.

.PARAMETER SearchDepth
    Number of AzDo commits to fetch when searching for the GitHub SHA. Default: 50.

.EXAMPLE
    ./eng/Get-BranchMirrorStatus.ps1
    ./eng/Get-BranchMirrorStatus.ps1 -SearchDepth 100
    ./eng/Get-BranchMirrorStatus.ps1 > status.md
#>

[CmdletBinding()]
param(
    [ValidateRange(1, [int]::MaxValue)]
    [int]$SearchDepth = 50
)

$ErrorActionPreference = 'Continue'

$AzDoOrg = "https://dev.azure.com/dnceng"

# Well-known Azure DevOps token scope GUID.
# See https://learn.microsoft.com/rest/api/azure/devops/tokens
$AzDoResourceId = "499b84ac-1321-427f-aa17-267ca6975798"

# Branch mappings for GitHub-to-Azure-DevOps mirror status checks.
$Mappings = @(
    @{ GHOrg = 'dotnet'; GHRepo = 'sdk'; GHBranch = 'release/8.0.1xx'; AzDoProject = 'internal'; AzDoRepo = 'dotnet-sdk'; AzDoBranch = 'internal/release/8.0.1xx' }
    @{ GHOrg = 'dotnet'; GHRepo = 'sdk'; GHBranch = 'release/8.0.4xx'; AzDoProject = 'internal'; AzDoRepo = 'dotnet-sdk'; AzDoBranch = 'internal/release/8.0.4xx' }
    @{ GHOrg = 'dotnet'; GHRepo = 'sdk'; GHBranch = 'release/9.0.1xx'; AzDoProject = 'internal'; AzDoRepo = 'dotnet-sdk'; AzDoBranch = 'internal/release/9.0.1xx' }
    @{ GHOrg = 'dotnet'; GHRepo = 'sdk'; GHBranch = 'release/9.0.3xx'; AzDoProject = 'internal'; AzDoRepo = 'dotnet-sdk'; AzDoBranch = 'internal/release/9.0.3xx' }
    @{ GHOrg = 'dotnet'; GHRepo = 'installer'; GHBranch = 'release/8.0.1xx'; AzDoProject = 'internal'; AzDoRepo = 'dotnet-installer'; AzDoBranch = 'internal/release/8.0.1xx' }
    @{ GHOrg = 'dotnet'; GHRepo = 'installer'; GHBranch = 'release/8.0.4xx'; AzDoProject = 'internal'; AzDoRepo = 'dotnet-installer'; AzDoBranch = 'internal/release/8.0.4xx' }
)

function Format-TableCell {
    param([string]$Text, [int]$MaxLength = 80)

    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $Text = $Text -replace '\r?\n', ' '
    if ($Text.Length -gt $MaxLength) {
        $Text = $Text.Substring(0, $MaxLength - 3) + '...'
    }
    return $Text
}

function Get-AzDoAuthHeaders {
    $tokenOutput = az account get-access-token --resource $AzDoResourceId --query accessToken -o tsv --only-show-errors
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    $token = $tokenOutput.Trim()
    return @{ Authorization = "Bearer $token" }
}

function Test-Prerequisites {
    $errors = @()

    # Check gh CLI
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        $errors += "``gh`` CLI is not installed. Install from https://cli.github.com/"
    }
    else {
        # Check status code while suppressing all output
        $null = gh auth status 2>&1
        if ($LASTEXITCODE -ne 0) {
            $errors += "``gh`` CLI is not authenticated. Run ``gh auth login``."
        }
    }

    # Check az CLI and AzDo access
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        $errors += "``az`` CLI is not installed. Install from https://aka.ms/install-az-cli"
    }
    else {
        $script:AzDoHeaders = Get-AzDoAuthHeaders
        if (-not $script:AzDoHeaders) {
            $errors += "Cannot acquire Azure DevOps token. Run ``az login`` and ensure you have access to the dnceng organization."
        }
        else {
            # Probe actual AzDo access
            $probeUrl = "$AzDoOrg/internal/_apis/git/repositories?api-version=7.1&`$top=1"
            try {
                $null = Invoke-RestMethod -Uri $probeUrl -Headers $script:AzDoHeaders -Method Get
            }
            catch {
                $errors += "Cannot access Azure DevOps (dnceng/internal). Run ``az login`` and ensure you have access to the dnceng organization."
            }
        }
    }

    if ($errors.Count -gt 0) {
        Write-Output "## Prerequisites Failed"
        Write-Output ""
        foreach ($e in $errors) {
            Write-Output "- $e"
        }
        exit 1
    }
}

function Get-GitHubLatestCommit {
    param(
        [string]$Org,
        [string]$Repo,
        [string]$Branch
    )

    $rawJson = gh api "repos/$Org/$Repo/commits?sha=$Branch&per_page=1" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API error for $Org/$Repo ($Branch): $rawJson"
    }
    $commits = @($rawJson | ConvertFrom-Json)
    if ($commits.Count -eq 0) {
        throw "No commits found on $Org/$Repo ($Branch)"
    }
    $c = $commits[0]
    return @{
        sha     = $c.sha
        message = $c.commit.message
    }
}

function Get-AzDoCommits {
    param(
        [string]$Project,
        [string]$Repo,
        [string]$Branch,
        [int]$Top
    )

    # https://learn.microsoft.com/rest/api/azure/devops/git/commits/get-commits
    $url = "$AzDoOrg/$Project/_apis/git/repositories/$Repo/commits" +
        "?searchCriteria.itemVersion.version=$Branch" +
        "&searchCriteria.itemVersion.versionType=branch" +
        "&`$top=$Top" +
        "&api-version=7.1"

    try {
        $result = Invoke-RestMethod -Uri $url -Headers $script:AzDoHeaders -Method Get
    }
    catch {
        throw "AzDo API error for $Project/$Repo ($Branch): $_"
    }
    return @($result.value)
}

function Test-MirrorStatus {
    param(
        [hashtable]$Mapping,
        [string]$GitHubSha
    )

    $commits = Get-AzDoCommits `
        -Project $Mapping.AzDoProject `
        -Repo $Mapping.AzDoRepo `
        -Branch $Mapping.AzDoBranch `
        -Top $SearchDepth

    if ($commits.Count -eq 0) {
        return @{ Mirrored = $false; Details = "AzDo branch has no commits or does not exist" }
    }

    # Check for direct push: any AzDo commit has the same SHA as the GitHub commit
    foreach ($c in $commits) {
        if ($c.commitId -eq $GitHubSha) {
            return @{ Mirrored = $true; Details = "Direct push (same SHA on AzDo branch)" }
        }
    }

    # Check for merge commit: any AzDo commit message references the GitHub SHA
    $escapedSha = [regex]::Escape($GitHubSha)
    foreach ($c in $commits) {
        if ($c.comment -match $escapedSha) {
            return @{ Mirrored = $true; Details = "Merge commit references SHA" }
        }
    }

    # Not mirrored — report the latest AzDo commit for context
    $latest = $commits[0]
    $latestSha = $latest.commitId.Substring(0, 7)
    $latestDate = if ($latest.author.date) { ([datetime]$latest.author.date).ToString('yyyy-MM-dd HH:mm') } else { 'unknown' }
    return @{
        Mirrored = $false
        Details  = "AzDo tip: ``$latestSha`` ($latestDate)"
    }
}

# --- Main ---

Test-Prerequisites

$results = @()

foreach ($m in $Mappings) {
    $repo = "$($m.GHOrg)/$($m.GHRepo)"
    $branch = $m.GHBranch

    try {
        $ghCommit = Get-GitHubLatestCommit -Org $m.GHOrg -Repo $m.GHRepo -Branch $m.GHBranch
        $shortSha = $ghCommit.sha.Substring(0, 7)
        $firstLine = ($ghCommit.message -split '\r?\n')[0]
        $commitDisplay = "``$shortSha`` $(Format-TableCell $firstLine 60)"

        $mirror = Test-MirrorStatus -Mapping $m -GitHubSha $ghCommit.sha
        $status = if ($mirror.Mirrored) { '✅' } else { '❌' }
        $details = Format-TableCell $mirror.Details

        $results += @{ Repo = $repo; Branch = $branch; Commit = $commitDisplay; Status = $status; Details = $details }
    }
    catch {
        $errMsg = Format-TableCell "$_"
        $results += @{ Repo = $repo; Branch = $branch; Commit = 'ERROR'; Status = '⚠️'; Details = $errMsg }
    }
}

# Emit markdown table
Write-Output "## Branch Mirror Status"
Write-Output ""
Write-Output "| Repo | Branch | Latest GH Commit | Mirrored? | Details |"
Write-Output "|---|---|---|---|---|"

foreach ($r in $results) {
    Write-Output "| $($r.Repo) | $($r.Branch) | $($r.Commit) | $($r.Status) | $($r.Details) |"
}
