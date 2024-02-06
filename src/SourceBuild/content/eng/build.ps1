[CmdletBinding(PositionalBinding=$false)]
Param(
  # Common settings
  [switch][Alias('bl')]$binaryLog,
  [string][Alias('c')]$configuration = "Release",
  [string][Alias('v')]$verbosity = "minimal",

  # Actions
  [switch]$clean,
  [switch][Alias('h')]$help,

  # Advanced settings
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
  Write-Host "  -help                   Print help and exit (short: -h)"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -ci                     Set when running on CI server"
  Write-Host "  -cleanWhileBuilding     Cleans each repo after building (reduces disk space usage, short: -cwb)"
  Write-Host "  -excludeCIBinarylog     Don't output binary log (short: -nobl)"
  Write-Host "  -prepareMachine         Prepare machine for CI run, clean up processes after build"
  Write-Host ""
}

. $PSScriptRoot\common\tools.ps1

# Set the NUGET_PACKAGES dir so that we don't accidentally pull some packages from the global location,
# They should be pulled from the local feeds.
$env:NUGET_PACKAGES="$RepoRoot\prereqs\packages\restored\"

if ($help) {
  Get-Usage
  exit 0
}

function Build {
  InitializeToolset

  $bl = if ($binaryLog) { '/bl:' + (Join-Path $LogDir 'Build.binlog') } else { '' }
  $cwb = if ($cleanWhileBuilding) { '/p:CleanWhileBuilding=true' } else { '' }
  $buildProj = Join-Path $RepoRoot 'build.proj'

  MSBuild $buildProj `
    $bl `
    /p:Configuration=$configuration `
    $cwb `
    @properties
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
