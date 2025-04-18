[CmdletBinding(PositionalBinding=$false)]
Param(
  [string][Alias('v')]$verbosity = "minimal",
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

$useGlobalNuGetCache=$false
$ci = $true

. $PSScriptRoot\common\tools.ps1

$project = Join-Path $EngRoot "join-verticals.proj"
$arguments = @()
$targets = "/t:JoinVerticals"

try {
  $bl = '/bl:' + (Join-Path $LogDir 'JoinVerticals.binlog')

  MSBuild -restore `
    $project `
    $bl `
    $targets `
    /p:Configuration=Release `
    @properties `
    @arguments
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Build' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
