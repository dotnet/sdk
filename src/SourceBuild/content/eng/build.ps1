[CmdletBinding(PositionalBinding=$false)]
Param(
  [string][Alias('c')]$configuration = "Release",
  [switch] $clean,
  [switch][Alias('bl')]$binaryLog,
  [switch][Alias('nobl')]$excludeCIBinarylog,
  [switch] $ci,
  [switch] $prepareMachine,
  [switch] $help,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

function Get-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -configuration <value>  Build configuration: 'Debug' or 'Release' (short: -c). [Default: Release]"
  Write-Host "  -binaryLog              Output binary log (short: -bl)"
  Write-Host "  -help                   Print help and exit"
  Write-Host ""

  Write-Host "Actions:"
  Write-Host "  -clean                  Clean the solution"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -ci                     Set when running on CI server"
  Write-Host "  -excludeCIBinarylog     Don't output binary log (short: -nobl)"
  Write-Host "  -prepareMachine         Prepare machine for CI run, clean up processes after build"
  Write-Host ""
}

. $PSScriptRoot\common\tools.ps1

# Set the NUGET_PACKAGES dir so that we don't accidentally pull some packages from the global location,
# They should be pulled from the local feeds.
$env:NUGET_PACKAGES="$PSScriptRoot\prereqs\packages\restored\"

function Build {
  InitializeToolset

  $bl = if ($binaryLog) { '/bl:' + (Join-Path $LogDir 'Build.binlog') } else { '' }

  MSBuild "$PSScriptRoot\build.proj" `
    $bl `
    --tl:off `
    /p:Configuration=$configuration `
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

  if ($help -or (($null -ne $properties) -and ($properties.Contains('/help') -or $properties.Contains('/?')))) {
    Print-Usage
    exit 0
  }

  if ($ci) {
    if (-not $excludeCIBinarylog) {
      $binaryLog = $true
    }
    $nodeReuse = $false
  }

  Build
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Build' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
