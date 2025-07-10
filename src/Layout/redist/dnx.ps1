# dnx.ps1
# PowerShell script to launch dotnet.exe with 'dnx' and all passed arguments
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$dotnet = Join-Path $scriptDir 'dotnet.exe'
& $dotnet dnx @Args
exit $LASTEXITCODE
