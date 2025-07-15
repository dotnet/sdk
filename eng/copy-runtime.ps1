# This script copies the downloaded runtime into the Program Files directory.
# This is necessary in certain situations when running tests in Visual Studio.

# Check if script is running as admin
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Write-Host "This script must be run as an administrator." -ForegroundColor Red
  exit 1
}

# Read the runtime version.
[xml]$versionPropsXml = Get-Content "$PSScriptRoot\Versions.props"
# In XPath expression syntax, `//` means, "select the node that matches regardless of the location".
$runtimeVersion = $versionPropsXml.SelectSingleNode("//MicrosoftNETCorePlatformsPackageVersion").InnerText

# Check if the repo was built.
$runtimeRepoPath = "$PSScriptRoot\..\.dotnet\shared\Microsoft.NETCore.App\$runtimeVersion"
if (-not (Test-Path -Path $runtimeRepoPath)) {
  Write-Host "The repo has not been built. Please run 'build.cmd' from the repo root." -ForegroundColor Red
  exit 1
}

# Copy the runtime to the Program Files if it doesn't exist.
$runtimeProgramFilesPath = "$env:ProgramFiles\dotnet\shared\Microsoft.NETCore.App\$runtimeVersion"
if (-not (Test-Path -Path $runtimeProgramFilesPath)) {
  Copy-Item -Path $runtimeRepoPath -Destination $runtimeProgramFilesPath -Recurse
  Write-Host "Runtime copied to: $runtimeProgramFilesPath"
}