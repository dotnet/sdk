# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$directory = Join-Path ([IO.Path]::GetTempPath()) "browser-trx-contract-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $directory | Out-Null
$required = Join-Path $directory 'required.json'
$trx = Join-Path $directory 'results.trx'
$validator = Join-Path $PSScriptRoot 'AssertBrowserTestResults.ps1'
'["BrowserRun", "BrowserCancellation"]' | Set-Content -LiteralPath $required

function Write-Results([string] $Rows, [int] $Total = 2, [int] $Executed = 2, [int] $Passed = 2) {
    @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>$Rows</Results>
  <ResultSummary outcome="Completed">
    <Counters total="$Total" executed="$Executed" passed="$Passed" failed="0" error="0" notExecuted="0" />
  </ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $trx
}

function Expect-Rejection([string] $Case) {
    $rejected = $false
    try {
        & $validator -Trx $trx -RequiredTests $required
    }
    catch {
        $rejected = $true
        Write-Host "$Case rejected: $($_.Exception.Message)"
    }
    if (-not $rejected) {
        throw "False success for $Case."
    }
}

try {
    $success = '<UnitTestResult testName="BrowserRun" outcome="Passed" /><UnitTestResult testName="BrowserCancellation" outcome="Passed" />'
    Write-Results $success
    & $validator -Trx $trx -RequiredTests $required

    foreach ($outcome in @('NotExecuted', 'Inconclusive', 'Failed', 'Aborted')) {
        Write-Results "<UnitTestResult testName=`"BrowserRun`" outcome=`"Passed`" /><UnitTestResult testName=`"BrowserCancellation`" outcome=`"$outcome`" />"
        Expect-Rejection $outcome
    }
    Write-Results '<UnitTestResult testName="BrowserRun" outcome="Passed" />' 1 1 1
    Expect-Rejection 'missing contract test'
    Write-Results '<UnitTestResult testName="BrowserRun" outcome="Passed" /><UnitTestResult testName="BrowserRun" outcome="Passed" />'
    Expect-Rejection 'duplicate contract test'
    Write-Results '' 0 0 0
    Expect-Rejection 'zero-test run'
    Write-Results $success 2 1 1
    Expect-Rejection 'inconsistent counters'
    Write-Results $success
    (Get-Content -LiteralPath $trx -Raw).Replace('outcome="Completed"', 'outcome="Failed"') |
        Set-Content -LiteralPath $trx
    Expect-Rejection 'failed summary with passing test rows'
    'not XML' | Set-Content -LiteralPath $trx
    Expect-Rejection 'malformed TRX'
    Remove-Item -LiteralPath $trx
    Expect-Rejection 'missing TRX'

    $previousLocation = (Get-Location).Path
    $previousServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
    $previousRoot = $env:DOTNET_ROOT
    $failedEvidence = Join-Path $directory 'failed-preflight'
    $rejected = $false
    try {
        & (Join-Path $PSScriptRoot 'RunBrowserTests.ps1') -SdkRoot $directory `
            -BrowserExecutable (Get-Process -Id $PID).Path -ResultsDirectory $failedEvidence
    }
    catch {
        $rejected = $true
    }
    if (-not $rejected -or (Get-Location).Path -ne $previousLocation -or
        $env:DOTNET_CLI_USE_MSBUILD_SERVER -ne $previousServer -or $env:DOTNET_ROOT -ne $previousRoot) {
        throw 'Failed preflight must reject invalid source and restore the exact location/environment.'
    }
    $evidence = Get-Content (Join-Path $failedEvidence 'evidence.json') -Raw | ConvertFrom-Json
    if ($evidence.succeeded -ne $false) { throw 'Failed preflight emitted success evidence.' }

    & {
        function Set-Content { throw [IO.IOException]::new('Simulated evidence write failure.') }
        $caught = $false
        try {
            & (Join-Path $PSScriptRoot 'RunBrowserTests.ps1') -SdkRoot $directory `
                -BrowserExecutable (Get-Process -Id $PID).Path `
                -ResultsDirectory (Join-Path $directory 'unwritable-evidence')
        }
        catch {
            if ($_.Exception.Message -ne 'Simulated evidence write failure.') { throw }
            $caught = $true
        }
        if (-not $caught -or (Get-Location).Path -ne $previousLocation -or
            $env:DOTNET_CLI_USE_MSBUILD_SERVER -ne $previousServer -or $env:DOTNET_ROOT -ne $previousRoot) {
            throw 'Evidence-writing failure leaked source location or environment state.'
        }
    }
    $cleanup = Join-Path $PSScriptRoot 'RemoveSdkIntermediates.ps1'
    $sdk = Join-Path $directory 'sdk'
    $obj = Join-Path $sdk 'artifacts\obj'
    $staging = Join-Path $sdk 'artifacts\bin\redist\Debug\net99.0'
    $layout = Join-Path $sdk 'artifacts\bin\redist\Debug\dotnet'
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $preserved = @(
        (Join-Path $sdk 'source.txt'), (Join-Path $sdk 'artifacts\log\build.log')
        (Join-Path $sdk 'artifacts\package-feed\package.nupkg'), (Join-Path $layout $dotnetName)
        (Join-Path $layout 'sdk\test\marker'), (Join-Path $sdk 'artifacts\btf\bin\testfx.dll')
    )
    foreach ($file in $preserved + @((Join-Path $obj 'marker'), (Join-Path $staging 'marker'))) {
        New-Item -ItemType Directory -Path (Split-Path $file) -Force | Out-Null
        'preserve' | Set-Content -LiteralPath $file
    }
    $caught = $false
    try {
        & (Join-Path $PSScriptRoot 'RunBrowserTests.ps1') -SdkRoot $sdk `
            -BrowserExecutable (Get-Process -Id $PID).Path -SkipSdkBuild -DiscardSdkIntermediates
    }
    catch {
        if ($_.Exception.Message -ne 'DiscardSdkIntermediates requires building the SDK in this run.') { throw }
        $caught = $true
    }
    if (-not $caught) { throw 'Discarding intermediates must not reuse a previous SDK build.' }
    foreach ($unsafe in @($layout, $sdk, $directory, '.')) {
        $caught = $false
        try { & $cleanup -SdkRoot $sdk -Configuration Debug -BuildOutputPath $unsafe }
        catch {
            if ($_.Exception.Message -notlike 'SDK staging output must be a sibling*') { throw }
            $caught = $true
        }
        if (-not $caught -or -not (Test-Path $obj) -or -not (Test-Path $staging)) {
            throw 'Unsafe staging paths must fail before deleting any intermediate output.'
        }
    }
    $linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
    foreach ($link in @((Join-Path $layout 'linked-obj'), (Join-Path $obj 'linked-staging'))) {
        $destination = if ($link.StartsWith($layout)) { $obj } else { $staging }
        New-Item -ItemType $linkType -Path $link -Value $destination | Out-Null
        try {
            $caught = $false
            try { & $cleanup -SdkRoot $sdk -Configuration Debug -BuildOutputPath $staging }
            catch {
                if ($_.Exception.Message -notlike '*links*') { throw }
                $caught = $true
            }
            if (-not $caught -or -not (Test-Path $obj) -or -not (Test-Path $staging)) {
                throw 'Linked intermediates must fail before deleting either directory.'
            }
        }
        finally { [IO.Directory]::Delete($link) }
    }
    $savedLayout = Join-Path $sdk 'saved-layout'
    Move-Item -LiteralPath $layout -Destination $savedLayout
    New-Item -ItemType $linkType -Path $layout -Value $savedLayout | Out-Null
    try {
        $caught = $false
        try { & $cleanup -SdkRoot $sdk -Configuration Debug -BuildOutputPath $staging }
        catch {
            if ($_.Exception.Message -notlike '*linked or non-directory path*') { throw }
            $caught = $true
        }
        if (-not $caught -or -not (Test-Path $obj) -or -not (Test-Path $staging)) {
            throw 'A linked shipping layout must fail before deleting either directory.'
        }
    }
    finally {
        [IO.Directory]::Delete($layout)
        Move-Item -LiteralPath $savedLayout -Destination $layout
    }
    & $cleanup -SdkRoot $sdk -Configuration Debug -BuildOutputPath $staging
    if ((Test-Path $obj) -or (Test-Path $staging)) { throw 'SDK intermediates were not discarded.' }
    foreach ($file in $preserved) {
        if ((Get-Content -LiteralPath $file -Raw).Trim() -ne 'preserve') {
            throw "Discarding intermediates changed retained content: $file"
        }
    }
    Write-Host 'Browser TRX contract checks passed (one positive and eleven negative cases).'
    Write-Host 'Preflight and evidence-writing failures restore process state.'
    Write-Host 'SDK cleanup preserves shipping/source/evidence files and rejects unsafe paths and links.'
    $global:LASTEXITCODE = 0
}
finally {
    Remove-Item -LiteralPath $directory -Recurse -Force
}
