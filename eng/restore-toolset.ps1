# Shared dotnetup acquisition helpers (architecture detection, cache freshness, download).
. (Join-Path $PSScriptRoot 'dotnetup-shared.ps1')

function Get-DotNetInstallFallbackArchitecture {
    if (-not [string]::IsNullOrEmpty($env:TARGET_ARCHITECTURE)) {
        $nativeArch = Get-NativeMachineArchitecture
        if ($env:TARGET_ARCHITECTURE -ne $nativeArch) {
            return $env:TARGET_ARCHITECTURE
        }
    }

    return ""
}

function InitializeCustomSDKToolset {
    if ($env:TestFullMSBuild -eq "true") {
        $env:DOTNET_SDK_TEST_MSBUILD_PATH = InitializeVisualStudioMSBuild -install:$true -vsRequirements:$GlobalJson.tools.'vs-opt'
        Write-Host "INFO: Tests will run against full MSBuild in $env:DOTNET_SDK_TEST_MSBUILD_PATH"
    }

    if (-not $restore) {
        return
    }

    # The following frameworks and tools are used only for testing.
    # Do not attempt to install them when building in the VMR.
    if ($fromVmr) {
        return
    }

    $cli = InitializeDotnetCli -install:$true

    # Redirect dotnetup data directory under artifacts so build scripts
    # don't read/write the user's home-folder manifest.
    $env:DOTNET_DOTNETUP_DATA_DIR = Join-Path $ArtifactsDir ".dotnetup"

    # The following shared frameworks are only needed for testing.
    # Set DOTNET_INSTALL_TEST_RUNTIMES=false to skip (e.g. cross-build containers with limited disk).
    if ($env:DOTNET_INSTALL_TEST_RUNTIMES -ne 'false') {
        $fallbackArchitecture = Get-DotNetInstallFallbackArchitecture
        $runtimeSpecs = @("6.0", "7.0", "8.0", "9.0", "10.0")
        if ([string]::IsNullOrEmpty($fallbackArchitecture)) {
            # Also install the exact runtime versions that arcade's toolset requires
            # (from Version.Details.props) so tests can target those specific versions.
            $runtimeSpecs += Get-CurrentRuntimeToolsetSpecs
        }
        InstallDotNetSharedFrameworks -RuntimeSpecs $runtimeSpecs -Architecture $fallbackArchitecture
    }

    CreateBuildEnvScripts
    CreateVSShortcut
    InstallNuget
}

function InstallNuGet {
    $NugetInstallDir = Join-Path $ArtifactsDir ".nuget"
    $NugetExe = Join-Path $NugetInstallDir "nuget.exe"

    if (!(Test-Path -Path $NugetExe)) {
        Create-Directory $NugetInstallDir
        Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -UseBasicParsing -OutFile $NugetExe
    }
}

function CreateBuildEnvScripts() {
    Create-Directory $ArtifactsDir
    $scriptPath = Join-Path $ArtifactsDir "sdk-build-env.bat"
    $scriptContents = @"
@echo off
title SDK Build ($RepoRoot)
REM https://aka.ms/vs/unsigned-dotnet-debugger-lib
set VSDebugger_ValidateDotnetDebugLibSignatures=0

set DOTNET_ROOT=$env:DOTNET_INSTALL_DIR
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$env:DOTNET_INSTALL_DIR

set PATH=$env:DOTNET_INSTALL_DIR;%PATH%
set NUGET_PACKAGES=$env:NUGET_PACKAGES
set DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0

DOSKEY killdotnet=taskkill /F /IM dotnet.exe /T ^& taskkill /F /IM VSTest.Console.exe /T ^& taskkill /F /IM msbuild.exe /T
"@

    Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII

    Create-Directory $ArtifactsDir
    $scriptPath = Join-Path $ArtifactsDir "sdk-build-env.ps1"
    $scriptContents = @"
`$host.ui.RawUI.WindowTitle = "SDK Build ($RepoRoot)"
# https://aka.ms/vs/unsigned-dotnet-debugger-lib
`$env:VSDebugger_ValidateDotnetDebugLibSignatures=0

`$env:DOTNET_ROOT="$env:DOTNET_INSTALL_DIR"
`$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR="$env:DOTNET_INSTALL_DIR"

`$env:PATH="$env:DOTNET_INSTALL_DIR;" + `$env:PATH
`$env:NUGET_PACKAGES="$env:NUGET_PACKAGES"
`$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH="0"

function killdotnet {
  taskkill /F /IM dotnet.exe /T
  taskkill /F /IM VSTest.Console.exe /T
  taskkill /F /IM msbuild.exe /T
}
"@

    Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII
}

function CreateVSShortcut() {
    # https://github.com/microsoft/vswhere/wiki/Installing
    $installerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    if (-Not (Test-Path -Path $installerPath)) {
        return
    }

    $versionFilePath = Join-Path $RepoRoot 'src\Layout\redist\minimumMSBuildVersion'
    # Gets the first digit (ex. 17) and appends '.0' to it.
    $vsMajorVersion = "$(((Get-Content $versionFilePath).Split('.'))[0]).0"
    $devenvPath = (& "$installerPath\vswhere.exe" -all -prerelease -latest -version $vsMajorVersion -find Common7\IDE\devenv.exe) | Select-Object -First 1
    if (-Not $devenvPath) {
        return
    }

    $scriptPath = Join-Path $ArtifactsDir 'sdk-build-env.ps1'
    $slnPath = Join-Path $RepoRoot 'sdk.slnx'
    $commandToLaunch = "& '$scriptPath'; & '$devenvPath' '$slnPath'"
    $powershellPath = '%SystemRoot%\system32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcutPath = Join-Path $ArtifactsDir 'VS with sdk.slnx.lnk'

    # https://stackoverflow.com/a/9701907/294804
    # https://learn.microsoft.com/en-us/troubleshoot/windows-client/admin-development/create-desktop-shortcut-with-wsh
    $wsShell = New-Object -ComObject WScript.Shell
    $shortcut = $wsShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $powershellPath
    $shortcut.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -Command ""$commandToLaunch"""
    $shortcut.IconLocation = $devenvPath
    $shortcut.WindowStyle = 7 # Minimized
    $shortcut.Save()
}

# Maps a dotnetup channel version (e.g. "9.0") to the specific version
# expected by the dotnet-install script's -Version parameter (e.g. "9.0.0").
# Full versions (e.g. "9.0.0-preview.5.24306.7") are passed through unchanged.
function ConvertTo-DotNetInstallScriptVersion([string]$version) {
    if ($version -match '^\d+\.\d+$') {
        return "$version.0"
    }

    return $version
}

function Get-VersionDetailsProperty([string]$propertyName) {
    $versionDetails = [xml](Get-Content -Raw -Path (Join-Path $RepoRoot 'eng\Version.Details.props'))
    $property = $versionDetails.SelectSingleNode("//$propertyName")
    if ($null -eq $property) {
        return ""
    }

    return $property.InnerText
}

function Get-CurrentRuntimeToolsetSpecs() {
    $runtimeVersion = Get-VersionDetailsProperty 'MicrosoftNETCoreAppRefPackageVersion'
    $aspNetCoreVersion = Get-VersionDetailsProperty 'MicrosoftAspNetCoreAppRefPackageVersion'

    $specs = @()
    if (-not [string]::IsNullOrEmpty($runtimeVersion)) {
        $specs += $runtimeVersion
    }
    if (-not [string]::IsNullOrEmpty($aspNetCoreVersion)) {
        $specs += "aspnetcore@$aspNetCoreVersion"
    }

    return $specs
}

# Maps a dotnetup component (e.g. 'aspnetcore', 'windowsdesktop' or 'dotnet')
# to the name of its shared-framework folder under <dotnet root>\shared.
function Get-SharedFrameworkName([string]$component) {
    switch ($component) {
        'aspnetcore' { return 'Microsoft.AspNetCore.App' }
        'windowsdesktop' { return 'Microsoft.WindowsDesktop.App' }
        default { return 'Microsoft.NETCore.App' }
    }
}

# Returns the shared-framework directory for a component
# (e.g. <dotnet root>\shared\Microsoft.AspNetCore.App).
function Get-SharedFrameworkPath([string]$dotNetRoot, [string]$component) {
    return Join-Path $dotNetRoot "shared\$(Get-SharedFrameworkName $component)"
}

# Tests whether a shared framework matching $version (a major.minor channel
# such as 6.0 or an exact version) is already present on disk for $component.
function Test-SharedFrameworkInstalled([string]$dotNetRoot, [string]$component, [string]$version) {
    $fxRoot = Get-SharedFrameworkPath $dotNetRoot $component

    # Only a major.minor channel (e.g. 6.0) should match any patch via a wildcard.
    # An exact version must match an exact folder so that, for example, 8.0.1 does
    # not spuriously match an installed 8.0.10.
    if ($version -match '^\d+\.\d+$') {
        return [bool](Test-Path -PathType Container (Join-Path $fxRoot "$version*"))
    }

    return [bool](Test-Path -PathType Container (Join-Path $fxRoot $version))
}

function InstallDotNetSharedFrameworks([string[]]$runtimeSpecs, [string]$architecture = "") {
    $dotnetRoot = $env:DOTNET_INSTALL_DIR

    # Skip if every requested framework is already on disk. Accept either a
    # dotnet runtime version/channel or a component@version spec such as
    # aspnetcore@11.0.0-preview.6. Treat major.minor channels as present if any
    # matching patch (e.g. 6.0.36) exists.
    $runtimeSpecsToInstall = @($runtimeSpecs | Where-Object {
            $component, $version = if ($_ -match '^([^@]+)@(.+)$') { $matches[1], $matches[2] } else { 'dotnet', $_ }
            -not (Test-SharedFrameworkInstalled $dotnetRoot $component $version)
        })
    if ($runtimeSpecsToInstall.Count -eq 0) {
        return
    }

    # dotnetup installs runtimes for its own process architecture and has no
    # architecture override (InstallerUtilities.GetDefaultInstallArchitecture uses
    # RuntimeInformation.ProcessArchitecture). On a cross-build (e.g. an x64 host
    # producing an arm64 test payload), dotnetup would silently install the host
    # architecture, so the test runtimes would not match the target Helix queue.
    # When a specific architecture is requested, use the dotnet-install script
    # directly since it honors -Architecture.
    if (-not [string]::IsNullOrEmpty($architecture)) {
        InstallDotNetSharedFrameworksWithInstallScript -RuntimeSpecs $runtimeSpecsToInstall -DotNetRoot $dotnetRoot -Architecture $architecture
        return
    }

    $dotnetupDir = Join-Path $PSScriptRoot "dotnetup"
    $dotnetupExe = Join-Path $dotnetupDir (GetExecutableFileName "dotnetup")

    if (-not (Test-ShouldUseCachedDotnetup $dotnetupExe)) {
        try {
            Install-DotnetupFromAkaMs $dotnetupDir
        }
        catch {
            Write-Host "Failed to acquire dotnetup ($($_.Exception.Message)); falling back to dotnet install script." -ForegroundColor Yellow
            InstallDotNetSharedFrameworksWithInstallScript -RuntimeSpecs $runtimeSpecsToInstall -DotNetRoot $dotnetRoot -Architecture $architecture
            return
        }
    }

    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    $installExitCode = Invoke-DotnetupNativeCommand {
        & $dotnetupExe runtime install @runtimeSpecsToInstall --install-path $dotnetRoot --set-default-install false --untracked --interactive false
    }

    if ($installExitCode -ne 0) {
        Write-Host "Failed to install shared frameworks ($($runtimeSpecsToInstall -join ', ')) to '$dotnetRoot' using dotnetup (exit code '$installExitCode'); falling back to dotnet install script." -ForegroundColor Yellow
        InstallDotNetSharedFrameworksWithInstallScript -RuntimeSpecs $runtimeSpecsToInstall -DotNetRoot $dotnetRoot -Architecture $architecture
    }
}

function InstallDotNetSharedFrameworksWithInstallScript([string[]]$runtimeSpecs, [string]$dotNetRoot, [string]$architecture = "") {
    $installScript = GetDotNetInstallScript $dotNetRoot
    foreach ($spec in $runtimeSpecs) {
        $component, $version = if ($spec -match '^([^@]+)@(.+)$') { $matches[1], $matches[2] } else { 'dotnet', $spec }
        $installVersion = ConvertTo-DotNetInstallScriptVersion $version
        $installArgs = @{
            Version               = $installVersion
            InstallDir            = $dotNetRoot
            Runtime               = $component
            SkipNonVersionedFiles = $true
        }
        if (-not [string]::IsNullOrEmpty($architecture)) {
            $installArgs.Architecture = $architecture
        }

        $global:LASTEXITCODE = 0
        & $installScript @installArgs
        $installScriptExitCode = $LASTEXITCODE

        $frameworkInstalled = Test-SharedFrameworkInstalled $dotNetRoot $component $version

        if ($installScriptExitCode -ne 0 -or -not $frameworkInstalled) {
            $architectureMessage = if ([string]::IsNullOrEmpty($architecture)) { "" } else { " for architecture '$architecture'" }
            throw "Failed to install shared framework $version to '$dotNetRoot' using dotnet install script$architectureMessage (exit code '$installScriptExitCode', installed '$frameworkInstalled')."
        }
    }
}

# Let's clear out the stage-zero folders that map to the current runtime to keep stage 2 clean
function CleanOutStage0ToolsetsAndRuntimes {
    $GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json
    $dotnetSdkVersion = $GlobalJson.tools.dotnet
    $dotnetRoot = $env:DOTNET_INSTALL_DIR
    $versionPath = Join-Path $dotnetRoot '.version'
    $aspnetRuntimePath = Get-SharedFrameworkPath $dotnetRoot 'aspnetcore'
    $coreRuntimePath = Get-SharedFrameworkPath $dotnetRoot 'dotnet'
    $wdRuntimePath = Get-SharedFrameworkPath $dotnetRoot 'windowsdesktop'
    $sdkPath = Join-Path $dotnetRoot 'sdk'
    $majorVersion = $dotnetSdkVersion.Split('.')[0]

    if (Test-Path($versionPath)) {
        $lastInstalledSDK = Get-Content -Raw -Path ($versionPath)
        if ($lastInstalledSDK -ne $dotnetSdkVersion) {
            $dotnetSdkVersion | Out-File -FilePath $versionPath -NoNewline
            Remove-Item (Join-Path $aspnetRuntimePath "$majorVersion.*") -Recurse
            Remove-Item (Join-Path $coreRuntimePath "$majorVersion.*") -Recurse
            Remove-Item (Join-Path $wdRuntimePath "$majorVersion.*") -Recurse
            Remove-Item (Join-Path $sdkPath "*") -Recurse
            Remove-Item (Join-Path $dotnetRoot "packs") -Recurse
            Remove-Item (Join-Path $dotnetRoot "sdk-manifests") -Recurse
            Remove-Item (Join-Path $dotnetRoot "templates") -Recurse
            throw "Installed a new SDK, deleting existing shared frameworks and sdk folders. Please rerun build"
        }
    }
    else {
        $dotnetSdkVersion | Out-File -FilePath $versionPath -NoNewline
    }
}

InitializeCustomSDKToolset

CleanOutStage0ToolsetsAndRuntimes
