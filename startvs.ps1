[CmdletBinding(PositionalBinding=$true)]
Param(
    [Parameter(
        Position=0,
        Mandatory=$false,
        HelpMessage="Solution file to open. The default is 'Razor.sln'.")]
    [string]$solutionFile=$null,

    [Parameter(
        Mandatory=$false,
        HelpMessage="If specified, choose the Visual Studio version from a list before laucnhing. By default the newest and last installed Visual Studio instance will be launched.")]
    [Switch]$chooseVS,

    [Parameter(
        Mandatory=$false,
        HelpMessage="If specified, Roslyn dependencies will be included in the Razor extension when deployed.")]
    [Switch]$includeRoslynDeps,

    [Parameter(
        Mandatory=$false,
        HelpMessage="If specified, bin logs will be enabled for VS. See https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Providing-Binary-Logs.md#capturing-binary-logs-through-visual-studio .")]
    [Switch]$captureBinLogs,

    [Parameter(
        Mandatory=$false,
        HelpMessage="If captureBinLogs is specified, this determines the output folder for them. Ignored if captureBinLogs is false.")]
    [string]$binLogFolder=$null
)

if ($solutionFile -eq "") {
    $solutionFile = "Razor.sln"
}

if ($includeRoslynDeps) {
    # Setting this environment variable ensures that the MSBuild will see it when
    # building from inside Visual Studio.
    $env:IncludeRoslynDeps = $true
}

$dotnetPath = Join-Path (Get-Location) ".dotnet"

# This tells .NET Core to use the same dotnet.exe that build scripts use
$env:DOTNET_ROOT = $dotnetPath
${env:DOTNET_ROOT(x86)} = Join-Path $dotnetPath "x86"

# This tells .NET Core not to go looking for .NET Core in other places
$env:DOTNET_MULTILEVEL_LOOKUP = 0

# Put our local dotnet.exe on PATH first so Visual Studio knows which one to use
$env:PATH = $env:DOTNET_ROOT + ";" + $env:PATH

if ($captureBinLogs) {
    $env:MSBUILDDEBUGENGINE = 1

    if ($binLogFolder -ne $null) {
        $env:MSBUILDDEBUGPATH=$binLogFolder
    }
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if ($chooseVS) {
    # Launch vswhere.exe to retrieve a list of installed Visual Studio instances
    $vsInstallsJson = &$vswhere -prerelease -format json
    $vsInstalls = $vsInstallsJson | ConvertFrom-Json

    # Display a menu of Visual Studio instances to the user
    Write-Host ""

    $index = 1

    foreach ($vsInstall in $vsInstalls) {
        $channelId = $vsInstall.installedChannelId
        $lastDotIndex = $channelId.LastIndexOf(".")
        $channelName = $channelId.Substring($lastDotIndex + 1);

        Write-Host "    $($index) - $($vsInstall.displayName) ($($vsInstall.installationVersion) - $($channelName))"
        $index += 1
    }

    Write-Host ""
    $choice = [int](Read-Host "Choose a Visual Studio version to launch")

    $vsBasePath = $vsInstalls[$choice - 1].installationPath
}
else {
    # Launch vswhere.exe to retrieve the newest, last installed Visual Studio instance
    $vsBasePath = &$vswhere -prerelease -latest -property installationPath
}

$vsPath = Join-Path $vsBasePath "Common7\IDE\devenv.exe"

Start-Process $vsPath -ArgumentList $solutionFile