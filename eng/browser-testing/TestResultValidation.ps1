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
    Write-Host 'Browser TRX contract checks passed (one positive and eleven negative cases).'
    Write-Host 'Preflight and evidence-writing failures restore process state.'
    $global:LASTEXITCODE = 0
}
finally {
    Remove-Item -LiteralPath $directory -Recurse -Force
}
