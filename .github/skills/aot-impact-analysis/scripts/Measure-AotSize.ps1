#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measures the NativeAOT binary-size impact of the current branch versus a baseline ref by
    publishing dotnet-aot on both sides and diffing the .mstat files with sizoscope-cli.

.DESCRIPTION
    Replicates the methodology of .github/workflows/aot-size-analysis.yml locally:
      1. Publishes src/Cli/dotnet-aot (Release, full ILC) for the current worktree (the "PR" side).
      2. Adds a detached git worktree at the baseline ref and publishes the same project there.
      3. Runs sizoscope-cli on the two dotnet-aot.mstat files to produce a per-symbol diff.
      4. Emits a Markdown summary (raw native dll delta + sizoscope "Total accounted size difference"
         + the full diff) ready to paste into a PR description.

    The script is idempotent and cleans up the temporary baseline worktree on exit.

.PARAMETER Rid
    Runtime identifier to publish (default: win-x64). CI publishes win-x64 and osx-arm64.

.PARAMETER Configuration
    Build configuration (default: Release - matches CI's buildConfiguration).

.PARAMETER BaseRef
    Git ref/commit to use as the baseline. Defaults to `git merge-base HEAD origin/main`
    (the PR fork point), which yields a diff of exactly this branch's additive cost.

.PARAMETER OutputPath
    Where to write the Markdown summary (default: <repo>/artifacts/aot-size-<rid>.md).

.PARAMETER SkipBaseline
    Reuse an existing baseline publish (skips the worktree add + baseline publish) if the
    baseline mstat is already present. Useful for re-running the diff after editing the report.

.EXAMPLE
    pwsh .github/skills/aot-impact-analysis/scripts/Measure-AotSize.ps1 -Rid win-x64
#>
[CmdletBinding()]
param(
    [string]$Rid = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$BaseRef,
    [string]$OutputPath,
    [switch]$SkipBaseline
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Find-RepoRoot {
    $dir = $PSScriptRoot
    while ($dir -and -not (Test-Path (Join-Path $dir '.git'))) {
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    if (-not $dir -or -not (Test-Path (Join-Path $dir '.git'))) {
        throw "Could not locate the repository root from $PSScriptRoot."
    }
    return (Resolve-Path $dir).Path
}

function Get-DotnetExe([string]$repoRoot) {
    $name = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $exe = Join-Path $repoRoot (Join-Path '.dotnet' $name)
    if (-not (Test-Path $exe)) {
        throw "Repo-local SDK not found at $exe. Run ./restore.cmd (or ./restore.sh) first."
    }
    return $exe
}

function Resolve-SizoscopeCli([string]$dotnet) {
    $toolsDir = Join-Path $HOME (Join-Path '.dotnet' 'tools')
    if ($env:PATH -notlike "*$toolsDir*") { $env:PATH = "$toolsDir$([IO.Path]::PathSeparator)$env:PATH" }
    if (Get-Command sizoscope-cli -ErrorAction SilentlyContinue) { return }
    Write-Host '==> Installing sizoscope-cli (global tool)...' -ForegroundColor Cyan
    & $dotnet tool install --global sizoscope-cli | Out-Host
    if (-not (Get-Command sizoscope-cli -ErrorAction SilentlyContinue)) {
        throw 'sizoscope-cli is not on PATH after install. Open a new shell or check ~/.dotnet/tools.'
    }
}

function Publish-DotnetAot([string]$dotnet, [string]$projectRoot, [string]$rid, [string]$config, [string]$label) {
    $proj = Join-Path $projectRoot 'src/Cli/dotnet-aot/dotnet-aot.csproj'
    Write-Host "==> Publishing dotnet-aot ($label): $config / $rid" -ForegroundColor Cyan
    $binlog = Join-Path $projectRoot "aot-size-$label.binlog"
    & $dotnet publish $proj -c $config -r $rid "/bl:$binlog" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $label (exit $LASTEXITCODE). Inspect $binlog with the binlog tools." }
    Remove-Item $binlog -ErrorAction SilentlyContinue
}

function Find-Native([string]$root, [string]$config, [string]$leaf) {
    if ($leaf -eq 'dotnet-aot.dll') {
        $base = Join-Path $root "artifacts/bin/dotnet-aot/$config"
    } else {
        $base = Join-Path $root "artifacts/obj/dotnet-aot/$config"
    }
    $hit = Get-ChildItem -Path $base -Recurse -Filter $leaf -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'native' } | Select-Object -First 1
    if (-not $hit) { throw "Could not find native/$leaf under $base." }
    return $hit.FullName
}

$repoRoot = Find-RepoRoot
$dotnet = Get-DotnetExe $repoRoot
if (-not $BaseRef) {
    $BaseRef = (& git -C $repoRoot merge-base HEAD origin/main).Trim()
}
$baseShort = $BaseRef.Substring(0, [Math]::Min(8, $BaseRef.Length))
if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot "artifacts/aot-size-$Rid.md" }
$baseWorktree = Join-Path (Split-Path $repoRoot -Parent) "_aot-size-base-$Rid"

Write-Host "Repo:      $repoRoot"
Write-Host "Base ref:  $BaseRef ($baseShort)"
Write-Host "RID:       $Rid    Config: $Configuration"

Resolve-SizoscopeCli $dotnet

# --- PR side (current worktree) ---
Publish-DotnetAot $dotnet $repoRoot $Rid $Configuration 'pr'
$prMstat = Find-Native $repoRoot $Configuration 'dotnet-aot.mstat'
$prDll = Find-Native $repoRoot $Configuration 'dotnet-aot.dll'

# --- Baseline side (temporary detached worktree) ---
$createdWorktree = $false
try {
    if (-not ($SkipBaseline -and (Test-Path $baseWorktree))) {
        if (Test-Path $baseWorktree) {
            & git -C $repoRoot worktree remove --force $baseWorktree 2>$null
        }
        Write-Host "==> Adding baseline worktree at $baseShort" -ForegroundColor Cyan
        & git -C $repoRoot worktree add --detach $baseWorktree $BaseRef | Out-Host
        $createdWorktree = $true
        Publish-DotnetAot $dotnet $baseWorktree $Rid $Configuration 'base'
    }
    $baseMstat = Find-Native $baseWorktree $Configuration 'dotnet-aot.mstat'
    $baseDll = Find-Native $baseWorktree $Configuration 'dotnet-aot.dll'

    # --- Diff ---
    $diffFile = Join-Path $repoRoot "artifacts/aot-size-$Rid.sizoscope.txt"
    Write-Host '==> Running sizoscope-cli diff...' -ForegroundColor Cyan
    & sizoscope-cli "$baseMstat" "$prMstat" --output $diffFile | Out-Host
    if (-not (Test-Path $diffFile)) { throw 'sizoscope-cli did not produce an output file.' }

    $diffLines = Get-Content $diffFile
    $totalLine = ($diffLines | Select-Object -First 1)
    $baseLen = (Get-Item $baseDll).Length
    $prLen = (Get-Item $prDll).Length
    $delta = $prLen - $baseLen
    $pct = if ($baseLen) { $delta / $baseLen * 100 } else { 0 }
    $accounted = ($totalLine -replace '^Total accounted size difference:\s*', '')

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("## AOT size impact ($Rid)")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("Published ``dotnet-aot`` ($Configuration, ``$Rid``, full ILC) on this branch vs. base (``$baseShort``); ``.mstat`` files diffed with ``sizoscope-cli``.")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| Native `dotnet-aot.dll` | Size |')
    [void]$sb.AppendLine('| --- | --- |')
    [void]$sb.AppendLine(("| Base (``{0}``) | {1:N3} MB ({2:N0} B) |" -f $baseShort, ($baseLen / 1MB), $baseLen))
    [void]$sb.AppendLine(("| This branch | {0:N3} MB ({1:N0} B) |" -f ($prLen / 1MB), $prLen))
    [void]$sb.AppendLine(("| **Delta** | **{0:+0;-0} KB ({1:+0.00;-0.00}%)** |" -f ($delta / 1KB), $pct))
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("``sizoscope-cli`` accounted difference: **$accounted**")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('<details><summary>sizoscope-cli diff</summary>')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('```')
    foreach ($l in $diffLines) { [void]$sb.AppendLine($l) }
    [void]$sb.AppendLine('```')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('</details>')

    Set-Content -Path $OutputPath -Value $sb.ToString() -Encoding utf8
    Write-Host ''
    Write-Host "Summary written to: $OutputPath" -ForegroundColor Green
    Write-Host ("Raw native dll delta: {0:+0;-0} KB ({1:+0.00;-0.00}%)" -f ($delta / 1KB), $pct) -ForegroundColor Green
    Write-Host "Accounted: $totalLine" -ForegroundColor Green
}
finally {
    if ($createdWorktree -and -not $SkipBaseline) {
        Write-Host '==> Removing baseline worktree...' -ForegroundColor Cyan
        & git -C $repoRoot worktree remove --force $baseWorktree 2>$null
    }
}
