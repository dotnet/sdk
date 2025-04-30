[CmdletBinding(PositionalBinding=$false)]
Param(
  # Common settings
  [switch][Alias('bl')]$binaryLog,
  [string][Alias('c')]$configuration = "Release",
  [string][Alias('rid')]$targetRid,
  [string][Alias('os')]$targetOS,
  [string][Alias('arch')]$targetArch,
  [string][Alias('v')]$verbosity = "minimal",

  # Actions
  [switch]$clean,
  [switch]$sign,
  [switch][Alias('h')]$help,
  [switch][Alias('t')]$test,

  # Advanced settings
  [switch]$buildRepoTests,
  [string]$projects,
  [switch]$ci,
  [switch][Alias('cwb')]$cleanWhileBuilding,
  [switch][Alias('nobl')]$excludeCIBinarylog,
  [switch] $prepareMachine,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

function Get-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -binaryLog                   Output binary log (short: -bl)"
  Write-Host "  -configuration <value>       Build configuration: 'Debug' or 'Release' (short: -c). [Default: Release]"
  Write-Host "  -rid, -targetRid <value>     Overrides the rid that is produced by the build. e.g. win-arm64, win-x64"
  Write-Host "  -os, -targetOS <value>       Target operating system: e.g. windows."
  Write-Host "  -arch, -targetArch <value>   Target architecture: e.g. x64, x86, arm64, arm, riscv64"
  Write-Host "  -verbosity <value>           Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic] (short: -v)"
  Write-Host ""

  Write-Host "Actions:"
  Write-Host "  -clean                  Clean the solution"
  Write-Host "  -sign                   Sign the build."
  Write-Host "  -help                   Print help and exit (short: -h)"
  Write-Host "  -test                   Run tests (repo tests omitted by default) (short: -t)"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -buildRepoTests         Build repository tests"
  Write-Host "  -projects <value>       Project or solution file to build"
  Write-Host "  -ci                     Set when running on CI server"
  Write-Host "  -cleanWhileBuilding     Cleans each repo after building (reduces disk space usage, short: -cwb)"
  Write-Host "  -excludeCIBinarylog     Don't output binary log (short: -nobl)"
  Write-Host "  -prepareMachine         Prepare machine for CI run, clean up processes after build"
  Write-Host ""
}

. $PSScriptRoot\common\tools.ps1

if ($help) {
  Get-Usage
  exit 0
}

$project = Join-Path $RepoRoot "build.proj"
$actions = @("/p:Restore=true", "/p:Build=true", "/p:Publish=true")

if ($test) {
  $project = Join-Path (Join-Path $RepoRoot "test") "tests.proj"
  $actions = @("/p:Restore=true", "/p:Build=true", "/p:Test=true", "/p:IsTestRun=true")

  # Workaround for vstest hangs (https://github.com/microsoft/vstest/issues/5091) [TODO]
  $env:MSBUILDENSURESTDOUTFORTASKPROCESSES="1"
}

$arguments = @()
# Override project if specified on cmd-line
if ($projects) {
  $project = $projects
}

if ($targetRid) {
  $arguments += "/p:TargetRid=$targetRid"
}

if ($targetOS) {
  $arguments += "/p:TargetOS=$targetOS"
}

if ($targetArch) {
  $arguments += "/p:TargetArchitecture=$targetArch"
}

if ($sign) {
  $arguments += "/p:DotNetBuildSign=true"
}

if ($buildRepoTests) {
  $arguments += "/p:DotNetBuildTests=true"
}

if ($cleanWhileBuilding) {
  $arguments += "/p:CleanWhileBuilding=true"
}

function Build {
  $buildProj = InitializeToolset

  $bl = if ($binaryLog) { '/bl:' + (Join-Path $LogDir 'Build.binlog') } else { '' }

  MSBuild -restore `
    $buildProj `
    /p:Projects=$project `
    /p:RepoRoot=$RepoRoot `
    "-tl:off" `
    $bl `
    /p:Configuration=$configuration `
    @actions `
    @properties `
    @arguments
}

try {
  if ($clean) {
    if (Test-Path $ArtifactsDir) {
      Remove-Item -Recurse -Force $ArtifactsDir
      Write-Host 'Artifacts directory deleted.'
    }
    exit 0
  }

  if ($ci) {
    if (-not $excludeCIBinarylog) {
      $binaryLog = $true
    }
  }

  Build
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Build' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
