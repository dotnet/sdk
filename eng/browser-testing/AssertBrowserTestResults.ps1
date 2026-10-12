# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Trx,
    [Parameter(Mandatory)][string] $RequiredTests
)

$ErrorActionPreference = 'Stop'
$required = @(Get-Content -LiteralPath $RequiredTests -Raw | ConvertFrom-Json)
if ($required.Count -eq 0 -or @($required | Sort-Object -Unique).Count -ne $required.Count -or
    @($required | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count) {
    throw 'The browser contract must name a nonempty, unique set of required tests.'
}

$settings = [System.Xml.XmlReaderSettings]::new()
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$settings.XmlResolver = $null
$reader = [System.Xml.XmlReader]::Create((Resolve-Path -LiteralPath $Trx).Path, $settings)
try {
    $document = [System.Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    $document.Load($reader)
}
finally {
    $reader.Dispose()
}

$results = @($document.SelectNodes("/*[local-name()='TestRun']/*[local-name()='Results']/*[local-name()='UnitTestResult']"))
$summary = $document.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']")
$counters = $document.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
if ($results.Count -eq 0 -or $null -eq $counters) {
    throw 'Browser acceptance did not produce nonempty TRX results and counters.'
}
if ($summary.outcome -notin @('Completed', 'Passed')) {
    throw "Browser acceptance summary was '$($summary.outcome)', not a completed passing run."
}

foreach ($result in $results) {
    if ($result.outcome -ne 'Passed') {
        throw "Required browser acceptance cannot skip or fail: '$($result.testName)' was '$($result.outcome)'."
    }
}
foreach ($name in $required) {
    $matches = @($results | Where-Object { $_.testName -ceq $name })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one executed result for '$name'; found $($matches.Count)."
    }
}
if ([int]$counters.total -ne $results.Count -or [int]$counters.executed -ne $results.Count -or
    [int]$counters.passed -ne $results.Count -or [int]$counters.failed -ne 0 -or
    [int]$counters.error -ne 0 -or [int]$counters.notExecuted -ne 0) {
    throw 'Browser acceptance counters do not describe an entirely executed, passing run.'
}

Write-Host "Verified $($results.Count) passing browser acceptance tests; all $($required.Count) contract cases ran without skips."
