[CmdletBinding(PositionalBinding=$false)]
Param(
  # Common settings
  [switch][Alias('bl')]$binaryLog,
  [string][Alias('c')]$configuration = "Release",
  [string][Alias('v')]$verbosity = "minimal",

  # Actions
  [switch]$clean,
  [switch]$sign,
  [switch][Alias('h')]$help,
  [switch][Alias('t')]$test,

  # Advanced settings
  [switch]$buildRepoTests,
  [switch]$ci,
  [switch][Alias('cwb')]$cleanWhileBuilding,
  [switch][Alias('nobl')]$excludeCIBinarylog,
  [switch] $prepareMachine,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

function Get-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -binaryLog              Output binary log (short: -bl)"
  Write-Host "  -configuration <value>  Build configuration: 'Debug' or 'Release' (short: -c). [Default: Release]"
  Write-Host "  -verbosity <value>      Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic] (short: -v)"
  Write-Host ""

  Write-Host "Actions:"
  Write-Host "  -clean                  Clean the solution"
  Write-Host "  -sign                   Sign the build."
  Write-Host "  -help                   Print help and exit (short: -h)"
  Write-Host "  -test                   Run tests (repo tests omitted by default) (short: -t)"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -buildRepoTests         Build repository tests"
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

# This repo uses the VSTest integration instead of the Arcade Test target
if ($test) {
  $project = Join-Path (Join-Path $RepoRoot "test") "tests.proj"
  $actions = @("/p:Restore=true", "/p:Build=true", "/p:Publish=true")

  # Workaround for vstest hangs (https://github.com/microsoft/vstest/issues/5091) [TODO]
  $env:MSBUILDENSURESTDOUTFORTASKPROCESSES="1"
}

$arguments = @()
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
    /p:DotNetPublishUsingPipelines=true `
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
