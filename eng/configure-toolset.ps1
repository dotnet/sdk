# We can't use already installed dotnet cli since we need to install additional shared runtimes.
# We could potentially try to find an existing installation that has all the required runtimes,
# but it's unlikely one will be available.

$script:useInstalledDotNetCli = $false

# Shared dotnetup acquisition helpers (architecture detection, cache freshness, download).
. (Join-Path $PSScriptRoot 'dotnetup-shared.ps1')

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
    $dotnetupExe = Join-Path $dotnetupDir (GetExecutableFileName 'dotnetup')

    if (-not (Test-ShouldUseCachedDotnetup $dotnetupExe)) {
        try {
            Install-DotnetupFromAkaMs $dotnetupDir
        }
        catch {
            Write-Host "Failed to acquire dotnetup: $($_.Exception.Message). Will fall back to standard dotnet-install script." -ForegroundColor Yellow
            return
        }
    }

    # Keep dotnetup's manifest under artifacts instead of the user's home dir.
    $env:DOTNET_DOTNETUP_DATA_DIR = Join-Path $ArtifactsDir '.dotnetup'

    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    $installExitCode = Invoke-DotnetupNativeCommand {
        & $dotnetupExe sdk install @sdkVersions `
            --install-path $dotnetRoot `
            --untracked `
            --set-default-install false `
            --interactive false
    }
    if ($installExitCode -ne 0) {
        Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "Failed to install .NET SDK(s) '$($sdkVersions -join ', ')' to '$dotnetRoot' using dotnetup (exit code '$installExitCode'). Will fall back to standard dotnet-install script."
        return
    }

    # Record the installed SDK so CleanOutStage0ToolsetsAndRuntimes does not
    # later treat this install as stale and wipe it (forcing a build rerun).
    Set-Content -Path (Join-Path $dotnetRoot '.version') -Value $dotnetSdkVersion -NoNewline
}

InstallBootstrapSdkWithDotnetup

