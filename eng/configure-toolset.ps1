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

    # Collect all SDK versions to install (primary + any additional).
    $sdkVersions = @($dotnetSdkVersion)
    if ($GlobalJson.tools.PSObject.Properties['additionalDotNetVersions']) {
        $sdkVersions += @($GlobalJson.tools.additionalDotNetVersions | Where-Object { -not [string]::IsNullOrEmpty($_) })
    }

    $dotnetRoot = Join-Path $RepoRoot '.dotnet'

    Write-Host "Installing SDK(s) '$($sdkVersions -join ', ')' to '$dotnetRoot' via dotnetup..." -ForegroundColor Cyan

    $dotnetupDir = Join-Path $PSScriptRoot 'dotnetup'
    $dotnetupExe = Join-Path $dotnetupDir 'dotnetup.exe'

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
        # Seed $LASTEXITCODE so strict mode can read it if the called script
        # short-circuits without invoking a native process.
        if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
        & (Join-Path $RepoRoot 'scripts\get-dotnetup.ps1') -InstallDir $dotnetupDir
        if ($LASTEXITCODE -ne 0) {
            Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "Failed to acquire dotnetup (exit code '$LASTEXITCODE')."
            ExitWithExitCode $LASTEXITCODE
        }
    }

    # Keep dotnetup's manifest under artifacts instead of the user's home dir.
    $env:DOTNET_DOTNETUP_DATA_DIR = Join-Path $ArtifactsDir '.dotnetup'

    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    & $dotnetupExe sdk install @sdkVersions `
        --install-path $dotnetRoot `
        --untracked `
        --set-default-install false `
        --interactive false
    if ($LASTEXITCODE -ne 0) {
        Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "Failed to install .NET SDK(s) '$($sdkVersions -join ', ')' to '$dotnetRoot' using dotnetup (exit code '$LASTEXITCODE')."
        ExitWithExitCode $LASTEXITCODE
    }

    # Record the installed SDK so CleanOutStage0ToolsetsAndRuntimes does not
    # later treat this install as stale and wipe it (forcing a build rerun).
    Set-Content -Path (Join-Path $dotnetRoot '.version') -Value $dotnetSdkVersion -NoNewline
}

InstallBootstrapSdkWithDotnetup

