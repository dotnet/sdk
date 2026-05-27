# We can't use already installed dotnet cli since we need to install additional shared runtimes.
# We could potentially try to find an existing installation that has all the required runtimes,
# but it's unlikely one will be available.

$script:useInstalledDotNetCli = $false

# Pre-install the bootstrap SDK pinned in global.json using dotnetup into the
# repo-local .dotnet directory that arcade's InitializeDotnetCli will pick up.
#
# Skipped during VMR / source-build (no network) and when -restore was not requested.
function InstallBootstrapSdkWithDotnetup() {
    $dotnetSdkVersion = $GlobalJson.tools.dotnet
    if ((-not $restore) -or $fromVmr -or [string]::IsNullOrEmpty($dotnetSdkVersion)) {
        return
    }

    # Skip if the pinned SDK is already present in either an externally provided
    # dotnet root or the repo-local one.
    $dotnetRoot = Join-Path $RepoRoot '.dotnet'
    foreach ($root in @($env:DOTNET_INSTALL_DIR, $dotnetRoot)) {
        if (-not [string]::IsNullOrEmpty($root) -and (Test-Path ([IO.Path]::Combine($root, 'sdk', $dotnetSdkVersion)))) {
            Write-Host "Bootstrap SDK '$dotnetSdkVersion' already present at '$root'; skipping dotnetup install." -ForegroundColor DarkGray
            Set-Content -Path (Join-Path $root '.version') -Value $dotnetSdkVersion -NoNewline
            return
        }
    }

    Write-Host "Installing bootstrap SDK '$dotnetSdkVersion' to '$dotnetRoot' via dotnetup..." -ForegroundColor Cyan

    $dotnetupDir = Join-Path $PSScriptRoot 'dotnetup'
    $dotnetupExe = Join-Path $dotnetupDir 'dotnetup.exe'

    # Seed $LASTEXITCODE so strict mode can read it if the called script
    # short-circuits without invoking a native process.
    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    # In CI, always pull the latest dotnetup so build agents don't reuse a
    # cached binary that predates fixes (e.g. the channel-parsing fix).
    if ($ci) { $env:DOTNETUP_FORCE_REINSTALL = '1' }
    & (Join-Path $RepoRoot 'scripts\get-dotnetup.ps1') -InstallDir $dotnetupDir
    if ($LASTEXITCODE -ne 0) {
        Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "Failed to acquire dotnetup (exit code '$LASTEXITCODE')."
        ExitWithExitCode $LASTEXITCODE
    }

    # Keep dotnetup's manifest under artifacts instead of the user's home dir.
    $env:DOTNET_DOTNETUP_DATA_DIR = Join-Path $ArtifactsDir '.dotnetup'

    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    & $dotnetupExe sdk install $dotnetSdkVersion `
        --install-path $dotnetRoot `
        --untracked `
        --set-default-install false `
        --interactive false
    if ($LASTEXITCODE -ne 0) {
        Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "Failed to install .NET SDK '$dotnetSdkVersion' to '$dotnetRoot' using dotnetup (exit code '$LASTEXITCODE')."
        ExitWithExitCode $LASTEXITCODE
    }

    # Record the installed SDK so CleanOutStage0ToolsetsAndRuntimes does not
    # later treat this install as stale and wipe it (forcing a build rerun).
    Set-Content -Path (Join-Path $dotnetRoot '.version') -Value $dotnetSdkVersion -NoNewline
}

InstallBootstrapSdkWithDotnetup

