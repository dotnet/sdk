# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $SdkRoot = (Join-Path $PSScriptRoot '..\..'),
    [string] $TestFxRoot,
    [Parameter(Mandatory)][string] $BrowserExecutable,
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [string] $ResultsDirectory,
    [switch] $SkipSdkBuild,
    [switch] $SkipTestFxBuild
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$SdkRoot = (Resolve-Path -LiteralPath $SdkRoot).Path
$BrowserExecutable = (Resolve-Path -LiteralPath $BrowserExecutable).Path
if (-not (Test-Path -LiteralPath $BrowserExecutable -PathType Leaf)) {
    throw 'BrowserExecutable must be an installed Chromium-family executable.'
}
$browserVersion = if ($IsWindows) {
    (Get-Item -LiteralPath $BrowserExecutable).VersionInfo.ProductVersion
}
else {
    $version = & $BrowserExecutable --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw 'The configured native browser cannot execute on this host.'
    }
    "$version"
}
if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $SdkRoot "artifacts\log\browser-integration\$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"
}
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
if (Test-Path -LiteralPath $ResultsDirectory) {
    throw 'Use a new results directory: existing TRX/package evidence must not make this run pass.'
}
New-Item -ItemType Directory -Path $ResultsDirectory | Out-Null
$commands = [Collections.Generic.List[object]]::new()
$environment = @{}
$runSucceeded = $false

function Invoke-Logged {
    param([string] $Name, [string] $Executable, [string[]] $Arguments, [switch] $PassThru)
    $started = [DateTime]::UtcNow
    $log = Join-Path $ResultsDirectory "$Name.log"
    Write-Host "[$Name] $Executable $($Arguments -join ' ')"
    $output = & $Executable @Arguments 2>&1 | Tee-Object -FilePath $log
    $code = $LASTEXITCODE
    $commands.Add(@{
        name = $Name; executable = $Executable; arguments = $Arguments
        workingDirectory = (Get-Location).Path; exitCode = $code
        startedUtc = $started.ToString('o'); completedUtc = [DateTime]::UtcNow.ToString('o')
    })
    if ($code -ne 0) {
        $output | Select-Object -Last 80 | Out-Host
        throw "$Name failed with exit $code. See $log"
    }
    if ($PassThru) {
        $output
    }
    else {
        $output | Select-Object -Last 20 | Out-Host
    }
}

function Set-ProcessEnvironment([string] $Name, $Value) {
    if ($null -eq $Value) {
        if (Test-Path -LiteralPath "Env:$Name") { Remove-Item -LiteralPath "Env:$Name" }
    }
    else {
        [Environment]::SetEnvironmentVariable($Name, [string]$Value)
    }
}

function Set-RunEnvironment([string] $Name, $Value) {
    if (-not $environment.ContainsKey($Name)) {
        $environment[$Name] = [Environment]::GetEnvironmentVariable($Name)
    }
    Set-ProcessEnvironment $Name $Value
}

function Get-Revision([string] $Root) {
    $revision = & git -C $Root rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') {
        throw "Cannot determine the exact source revision at $Root."
    }
    $changes = & git -C $Root status --porcelain --untracked-files=normal
    if ($LASTEXITCODE -ne 0 -or $changes) {
        throw "Commit source changes before composed validation; the checkout at $Root must match its reported revision."
    }
    return $revision.Trim()
}

function Read-Package([string] $Path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $nuspec = @($zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.FullName.EndsWith('.nuspec') })
        if ($nuspec.Count -ne 1) {
            throw "Expected one package manifest in $Path."
        }
        $reader = [IO.StreamReader]::new($nuspec[0].Open())
        try { [xml] $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $xml.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        return @{
            Id = $metadata.SelectSingleNode("*[local-name()='id']").InnerText
            Version = $metadata.SelectSingleNode("*[local-name()='version']").InnerText
            SourceCommit = $metadata.SelectSingleNode("*[local-name()='repository']").GetAttribute('commit')
        }
    }
    finally { $zip.Dispose() }
}

Push-Location $SdkRoot
try {
    Set-RunEnvironment 'MSBUILDDISABLENODEREUSE' '1'
    Set-RunEnvironment 'DOTNET_CLI_USE_MSBUILD_SERVER' '0'
    Set-RunEnvironment 'DOTNET_CLI_TELEMETRY_OPTOUT' '1'
    Set-RunEnvironment 'DOTNET_CLI_CONTEXT_VERBOSE' '0'
    Set-RunEnvironment 'DOTNET_CLI_CONTEXT_ANSI_PASS_THRU' '0'
    Set-RunEnvironment 'DOTNET_ROOT' $null
    Set-RunEnvironment 'DOTNET_HOST_PATH' $null
    Set-RunEnvironment 'MSBuildSDKsPath' $null
    $sdkRevision = Get-Revision $SdkRoot
    if (-not $TestFxRoot) {
        $pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'testfx.json') -Raw | ConvertFrom-Json
        if ($pin.repository -ne 'microsoft/testfx' -or $pin.commit -notmatch '^[0-9a-f]{40}$') {
            throw 'The TestFX counterpart must be a full SHA in microsoft/testfx.'
        }
        # Keep legacy Windows MSBuild task paths below MAX_PATH; identity is checked by full SHA.
        $TestFxRoot = Join-Path $SdkRoot "artifacts\btf-$($pin.commit.Substring(0, 8))"
        if (-not (Test-Path -LiteralPath $TestFxRoot)) {
            New-Item -ItemType Directory -Path $TestFxRoot | Out-Null
            Invoke-Logged 'peer-init' 'git' @('init', '--quiet', $TestFxRoot)
            Invoke-Logged 'peer-origin' 'git' @('-C', $TestFxRoot, 'remote', 'add', 'origin', "https://github.com/$($pin.repository).git")
            Invoke-Logged 'peer-fetch' 'git' @('-C', $TestFxRoot, 'fetch', '--depth', '1', 'origin', $pin.commit)
            Invoke-Logged 'peer-checkout' 'git' @('-C', $TestFxRoot, 'checkout', '--quiet', '--detach', 'FETCH_HEAD')
        }
        if ((Get-Revision $TestFxRoot) -ne $pin.commit) {
            throw 'The counterpart checkout does not match the pinned TestFX commit.'
        }
    }
    $TestFxRoot = (Resolve-Path -LiteralPath $TestFxRoot).Path
    if ($TestFxRoot -eq $SdkRoot) { throw 'SDK and TestFX require separate source checkouts.' }
    $testfxRevision = Get-Revision $TestFxRoot
    $requiredTests = Join-Path $TestFxRoot 'eng\browser-testing\required-tests.json'
    if (-not (Test-Path -LiteralPath $requiredTests)) {
        throw 'The TestFX source must include the strict browser acceptance contract.'
    }

    if (-not $SkipSdkBuild) {
        $redistProject = Join-Path 'src' 'Layout' 'redist' 'redist.csproj'
        if ($IsWindows) {
            Invoke-Logged 'sdk-build' (Join-Path $SdkRoot 'build.cmd') @('-c', $Configuration, '-projects', $redistProject)
        }
        else {
            Invoke-Logged 'sdk-build' 'bash' @((Join-Path $SdkRoot 'build.sh'), '--configuration', $Configuration, '--projects', $redistProject)
        }
    }
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $sdkLayout = Join-Path $SdkRoot "artifacts\bin\redist\$Configuration\dotnet"
    $sdkDotnet = Join-Path $sdkLayout $dotnetName
    $matchingSdks = @(Get-ChildItem (Join-Path $sdkLayout 'sdk') -Directory | Where-Object {
        $stamp = Join-Path $_.FullName '.toolsetversion'
        (Test-Path -LiteralPath $stamp) -and (Get-Content -LiteralPath $stamp -First 1) -eq $sdkRevision
    })
    if ($matchingSdks.Count -ne 1 -or -not (Test-Path -LiteralPath $sdkDotnet)) {
        throw 'Build the selected SDK source first; exactly one matching shipping layout is required.'
    }

    Set-Location $TestFxRoot
    if (-not $SkipTestFxBuild) {
        if ($IsWindows) {
            Invoke-Logged 'testfx-build-pack' (Join-Path $TestFxRoot 'build.cmd') @('-c', $Configuration, '-pack', '-bl')
        }
        else {
            Invoke-Logged 'testfx-build-pack' 'bash' @((Join-Path $TestFxRoot 'build.sh'), '--configuration', $Configuration, '--pack', '--binaryLog')
        }
    }
    $shipping = Join-Path $TestFxRoot "artifacts\packages\$Configuration\Shipping"
    $feed = Join-Path $ResultsDirectory 'packages'
    New-Item -ItemType Directory -Path $feed | Out-Null
    $packageIds = @(
        'Microsoft.Testing.Platform.Browser', 'Microsoft.Testing.Platform', 'Microsoft.Testing.Platform.MSBuild'
        'Microsoft.Testing.Extensions.Telemetry', 'Microsoft.Testing.Extensions.TrxReport.Abstractions'
        'MSTest.TestFramework', 'MSTest.TestAdapter', 'MSTest.Analyzers'
    )
    $manifest = @()
    foreach ($id in $packageIds) {
        $candidates = @(Get-ChildItem -LiteralPath $shipping -Filter "$id.*.nupkg" -File | Where-Object {
            $_.Name -match ('^' + [regex]::Escape($id) + '\.\d')
        })
        $selected = @($candidates | Where-Object { (Read-Package $_.FullName).SourceCommit -eq $testfxRevision })
        if ($selected.Count -ne 1) {
            throw "Expected exactly one $id package built from $testfxRevision; stale/mixed packages are not accepted."
        }
        $package = $selected[0]
        $metadata = Read-Package $package.FullName
        Copy-Item -LiteralPath $package.FullName -Destination $feed
        $manifest += @{
            Name = $package.Name; Length = $package.Length
            SHA256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
            SourceCommit = $metadata.SourceCommit
        }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $feed 'manifest.json')

    $bootstrap = Join-Path $TestFxRoot ".dotnet\$dotnetName"
    $acceptance = Join-Path $TestFxRoot 'test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj'
    $frameworkOutput = @(Invoke-Logged 'acceptance-framework' $bootstrap @('msbuild', $acceptance, '-nologo', "-p:Configuration=$Configuration", '-getProperty:NetCurrent') -PassThru)
    $framework = ($frameworkOutput | Where-Object { -not [string]::IsNullOrWhiteSpace("$_") } | Select-Object -Last 1).ToString().Trim()
    if ($framework -notmatch '^net\d+\.\d+$') { throw 'Unable to evaluate the acceptance target framework.' }
    Invoke-Logged 'acceptance-build' $bootstrap @('build', $acceptance, '-c', $Configuration, '-f', $framework, "-bl:$(Join-Path $ResultsDirectory 'acceptance-build.binlog')")
    $targetOutput = @(Invoke-Logged 'acceptance-target' $bootstrap @('msbuild', $acceptance, '-nologo', "-p:Configuration=$Configuration", "-p:TargetFramework=$framework", '-getProperty:TargetPath') -PassThru)
    $assembly = ($targetOutput | Where-Object { -not [string]::IsNullOrWhiteSpace("$_") } | Select-Object -Last 1).ToString().Trim()
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'The acceptance assembly was not built.' }

    Set-RunEnvironment 'TESTINGPLATFORM_BROWSER_REQUIRED' '1'
    Set-RunEnvironment 'TESTINGPLATFORM_BROWSER_SDK_DOTNET' $sdkDotnet
    Set-RunEnvironment 'TESTINGPLATFORM_BROWSER_SDK_VERSION' $matchingSdks[0].Name
    Set-RunEnvironment 'TESTINGPLATFORM_BROWSER_SDK_COMMIT' $sdkRevision
    Set-RunEnvironment 'TESTINGPLATFORM_BROWSER_PACKAGE_FEED' $feed
    Set-RunEnvironment 'TESTINGPLATFORM_BROWSER_EXECUTABLE' $BrowserExecutable
    $testResults = Join-Path $ResultsDirectory 'test-results'
    New-Item -ItemType Directory -Path $testResults | Out-Null
    Invoke-Logged 'browser-acceptance' $bootstrap @(
        'exec', $assembly, '--filter', 'FullyQualifiedName~BrowserPackageExecutionTests'
        '--report-trx', '--report-trx-filename', 'browser.trx', '--results-directory', $testResults, '--output', 'Detailed'
    )
    & (Join-Path $PSScriptRoot 'AssertBrowserTestResults.ps1') -Trx (Join-Path $testResults 'browser.trx') -RequiredTests $requiredTests
    foreach ($package in $manifest) {
        if ((Get-FileHash -LiteralPath (Join-Path $feed $package.Name) -Algorithm SHA256).Hash -ne $package.SHA256) {
            throw "Acceptance changed the immutable package snapshot: $($package.Name)"
        }
    }
    if ((Get-Revision $SdkRoot) -ne $sdkRevision -or (Get-Revision $TestFxRoot) -ne $testfxRevision) {
        throw 'A source checkout changed while the composed validation was running.'
    }
    $runSucceeded = $true
}
finally {
    try {
        @{
            succeeded = $runSucceeded; sdkCommit = $sdkRevision; testfxCommit = $testfxRevision
            sdkVersion = if ($matchingSdks.Count -eq 1) { $matchingSdks[0].Name } else { $null }
            browser = $BrowserExecutable; browserVersion = "$browserVersion"
            configuration = $Configuration; commands = $commands.ToArray()
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'evidence.json')
    }
    finally {
        foreach ($name in $environment.Keys) { Set-ProcessEnvironment $name $environment[$name] }
        Pop-Location
    }
    Write-Host "Browser integration evidence: $ResultsDirectory"
}
