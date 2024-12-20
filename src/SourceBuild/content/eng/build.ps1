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
  [switch] $dev,
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
  Write-Host "  -dev                    Use -dev or -ci versioning instead of .NET official build versions"
  Write-Host ""
}

$useGlobalNuGetCache=$false

. $PSScriptRoot\common\tools.ps1

if ($help) {
  Get-Usage
  exit 0
}

$project = Join-Path $RepoRoot "build.proj"
$arguments = @()
$targets = "/t:Build"

# This repo uses the VSTest integration instead of the Arcade Test target
if ($test) {
  $project = Join-Path (Join-Path $RepoRoot "test") "tests.proj"
  $targets += ";VSTest"
  # Workaround for vstest hangs (https://github.com/microsoft/vstest/issues/5091) [TODO]
  $env:MSBUILDENSURESTDOUTFORTASKPROCESSES="1"
}

if ($sign) {
  $arguments += "/p:Sign=true"
  # Force dry run signing for now. In typical VMR builds, the official build ID is set for each repo, which
  # tells the signing infra that it should expect to see signed bits. This won't be the case in CI builds,
  # and won't be the case for official builds until more of the real signing infra is functional.
  # https://github.com/dotnet/source-build/issues/4678
  $arguments +=  "/p:ForceDryRunSigning=true"
}

if ($buildRepoTests) {
  $arguments += "/p:DotNetBuildTests=true"
}

if ($cleanWhileBuilding) {
  $arguments += "/p:CleanWhileBuilding=true"
}

if ($dev) {
  $arguments += "/p:UseOfficialBuildVersioning=false"
}

function Build {
  InitializeToolset

  # Manually unset NUGET_PACKAGES as InitializeToolset sets it unconditionally.
  # The env var shouldn't be set so that the RestorePackagesPath msbuild property is respected.
  $env:NUGET_PACKAGES=''

  $bl = if ($binaryLog) { '/bl:' + (Join-Path $LogDir 'Build.binlog') } else { '' }

  MSBuild -restore `
    $project `
    $bl `
    $targets `
    /p:Configuration=$configuration `
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
