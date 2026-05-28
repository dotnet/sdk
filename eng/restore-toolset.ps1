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
        InstallDotNetSharedFrameworks "6.0", "7.0", "8.0", "9.0", "10.0"
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

function InstallDotNetSharedFrameworks([string[]]$versions) {
    $dotnetRoot = $env:DOTNET_INSTALL_DIR

    # Skip if every requested framework is already on disk. Accept either an
    # exact version or a major.minor channel; treat as present if any matching
    # patch (e.g. 6.0.36) exists under shared\Microsoft.NETCore.App.
    $fxRoot = Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App'
    $versionsToInstall = @($versions | Where-Object {
            -not (Test-Path -PathType Container (Join-Path $fxRoot "$_*"))
        })
    if ($versionsToInstall.Count -eq 0) {
        return
    }

    $dotnetupDir = Join-Path $PSScriptRoot "dotnetup"
    $dotnetupExe = Join-Path $dotnetupDir "dotnetup.exe"

    # Re-download dotnetup at most once every 24 hours to avoid unnecessary network calls.
    $skipDownload = $false
    if (Test-Path $dotnetupExe) {
        $age = (Get-Date) - (Get-Item $dotnetupExe).LastWriteTime
        if ($age.TotalHours -lt 24) {
            Write-Host "dotnetup binary is less than 24 hours old; skipping re-download." -ForegroundColor DarkGray
            $skipDownload = $true
        }
    }

    if (-not $skipDownload) {
        # Acquire the latest dotnetup daily build using the in-repo install script.
        # get-dotnetup.ps1 may short-circuit without invoking a native process,
        # leaving $LASTEXITCODE unset; seed it so strict mode can read it.
        if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
        & (Join-Path $RepoRoot "scripts\get-dotnetup.ps1") -InstallDir $dotnetupDir
    }

    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    & $dotnetupExe runtime install @versionsToInstall --install-path $dotnetRoot --set-default-install false --untracked --interactive false

    if ($lastExitCode -ne 0) {
        throw "Failed to install shared frameworks ($($versionsToInstall -join ', ')) to '$dotnetRoot' using dotnetup (exit code '$lastExitCode')."
    }
}

# Let's clear out the stage-zero folders that map to the current runtime to keep stage 2 clean
function CleanOutStage0ToolsetsAndRuntimes {
    $GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json
    $dotnetSdkVersion = $GlobalJson.tools.dotnet
    $dotnetRoot = $env:DOTNET_INSTALL_DIR
    $versionPath = Join-Path $dotnetRoot '.version'
    $aspnetRuntimePath = [IO.Path]::Combine( $dotnetRoot, 'shared' , 'Microsoft.AspNetCore.App')
    $coreRuntimePath = [IO.Path]::Combine( $dotnetRoot, 'shared' , 'Microsoft.NETCore.App')
    $wdRuntimePath = [IO.Path]::Combine( $dotnetRoot, 'shared', 'Microsoft.WindowsDesktop.App')
    $sdkPath = Join-Path $dotnetRoot 'sdk'
    $majorVersion = $dotnetSdkVersion.Substring(0, 1)

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
