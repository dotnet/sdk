# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$directory = Join-Path ([IO.Path]::GetTempPath()) "browser-trx-contract-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $directory | Out-Null
$required = Join-Path $directory 'required.json'
$trx = Join-Path $directory 'results.trx'
$validator = Join-Path $PSScriptRoot 'AssertBrowserTestResults.ps1'
. (Join-Path $PSScriptRoot 'RuntimePrerequisites.ps1')
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
    $bootstrap = Join-Path $directory 'bootstrap'
    $layout = Join-Path $directory 'sdk-layout'
    $runtime = '11.0.0-rc.2.12345.1'
    $sourceRuntime = Join-Path $bootstrap "shared\Microsoft.NETCore.App\$runtime"
    $targetRuntime = Join-Path $layout "shared\Microsoft.NETCore.App\$runtime"
    New-Item -ItemType Directory -Path $sourceRuntime -Force | Out-Null
    New-Item -ItemType Directory -Path $layout -Force | Out-Null
    'runtime input' | Set-Content -LiteralPath (Join-Path $sourceRuntime 'runtime.dll')
    'selected SDK' | Set-Content -LiteralPath (Join-Path $layout '.sdk-source')
    $runtimeManifest = @(Copy-BrowserRuntimePrerequisites -BootstrapRoot $bootstrap -SdkLayout $layout -Versions $runtime)
    if ($runtimeManifest.Count -ne 1 -or $runtimeManifest[0].Version -ne $runtime -or
        (Get-FileHash (Join-Path $sourceRuntime 'runtime.dll')).Hash -ne
        (Get-FileHash (Join-Path $targetRuntime 'runtime.dll')).Hash -or
        (Get-Content (Join-Path $layout '.sdk-source') -Raw).Trim() -ne 'selected SDK') {
        throw 'Runtime composition must preserve the exact restored bytes and selected SDK.'
    }
    'interrupted copy' | Set-Content -LiteralPath (Join-Path $targetRuntime 'runtime.dll')
    Copy-BrowserRuntimePrerequisites -BootstrapRoot $bootstrap -SdkLayout $layout -Versions $runtime | Out-Null
    if ((Get-FileHash (Join-Path $sourceRuntime 'runtime.dll')).Hash -ne
        (Get-FileHash (Join-Path $targetRuntime 'runtime.dll')).Hash) {
        throw 'A retried runtime composition must repair an interrupted copy.'
    }
    foreach ($invalid in @('12.0.0-missing', '../escape', '')) {
        $rejected = $false
        try {
            Copy-BrowserRuntimePrerequisites -BootstrapRoot $bootstrap -SdkLayout $layout -Versions @($invalid) | Out-Null
        }
        catch { $rejected = $true }
        if (-not $rejected) { throw "Runtime prerequisite '$invalid' must not be silently accepted." }
    }

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
    Write-Host 'Browser TRX contract checks passed (one positive and eleven negative cases).'
    Write-Host 'Preflight and evidence-writing failures restore process state.'
    Write-Host 'Runtime prerequisites preserve exact inputs, repair partial copies, and reject missing/invalid versions.'
    $global:LASTEXITCODE = 0
}
finally {
    Remove-Item -LiteralPath $directory -Recurse -Force
}
