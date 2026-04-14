[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch][Alias('h')]$help,
  [ValidateSet("Debug","Release")][string[]][Alias('c')]$configuration = @("Debug"),
  [ValidateSet("windows","linux","osx","freebsd")][string]$os,
  [ValidateSet("x86","x64","arm","arm64")][string][Alias('a')]$arch,
  [switch][Alias('t')]$test,
  [switch]$pack,
  [switch]$skipCrossgen,
  [switch]$skipInstallers,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

if ($help) {
  Write-Host "Common settings:"
  Write-Host "  -arch (-a)           Target architecture: x86, x64, arm, arm64 [default: x64]"
  Write-Host "  -configuration (-c)  Build configuration: Debug or Release [default: Debug]"
  Write-Host "  -os                  Target OS: windows, linux, osx, freebsd [default: host OS]"
  Write-Host "  -test (-t)           Run tests after building"
  Write-Host "  -pack                Build installers and packages"
  Write-Host ""
  Write-Host "Advanced:"
  Write-Host "  -skipCrossgen        Skip crossgen during layout generation"
  Write-Host "  -skipInstallers      Skip building installers"
  Write-Host ""
  Write-Host "Command-line arguments not listed above are passed through to MSBuild."
  exit 0
}

$arguments = "-restore -build -msbuildEngine dotnet"

if ($os)   { $arguments += " /p:TargetOS=$($os.ToLowerInvariant())" }
if ($arch) { $arguments += " /p:TargetArchitecture=$arch" }

if ($test) { $arguments += " -test" }
if ($pack) {
  $arguments += " -pack /p:SkipUsingCrossgen=false /p:SkipBuildingInstallers=false"
} else {
  if ($skipCrossgen -or -not $pack) {
    $arguments += " /p:SkipUsingCrossgen=true"
  }
  if ($skipInstallers -or -not $pack) {
    $arguments += " /p:SkipBuildingInstallers=true"
  }
}

if ($properties) { $arguments += " " + ($properties -join " ") }

$arguments += " /tlp:summary"
$arguments += " /graph"

$env:DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT = "true"

foreach ($config in $configuration) {
  $argumentsWithConfig = $arguments + " -configuration $config"
  Invoke-Expression "& `"$PSScriptRoot/common/build.ps1`" $argumentsWithConfig"
  if ($lastExitCode -ne 0) { exit $lastExitCode }
}

exit 0
