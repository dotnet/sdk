#!/usr/bin/env pwsh

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Assert-Matches {
    param(
        [Parameter(Mandatory)]
        [string] $Actual,

        [Parameter(Mandatory)]
        [string] $Pattern
    )

    if ($Actual -notmatch $Pattern) {
        throw "Expected output to match '$Pattern'."
    }
}

$tempDirectory = Join-Path ([IO.Path]::GetTempPath()) "aot-size-treemap-$([Guid]::NewGuid())"
[IO.Directory]::CreateDirectory($tempDirectory) | Out-Null

try {
    $diffPath = Join-Path $tempDirectory 'diff.txt'
    $svgPath = Join-Path $tempDirectory 'treemap.svg'
    $diff = @'
Total accounted size difference: 115.0 kB

=== New / Grown ===
  +129.0 kB     System.Private.CoreLib
  +52.4 kB      Example.Library & Tools

=== Removed / Shrunk ===
  -66.4 kB      Old.Library

  New frozen objects: +1.5 kB
  Removed blobs: -512 B
'@
    [IO.File]::WriteAllText($diffPath, $diff)

    & (Join-Path $PSScriptRoot 'generate-treemap.ps1') `
        -InputPath $diffPath `
        -OutputPath $svgPath `
        -Platform 'Linux_x64_AOT & test'

    $svg = [IO.File]::ReadAllText($svgPath)
    [xml] $null = $svg

    Assert-Matches -Actual $svg -Pattern '^<\?xml version="1\.0"'
    Assert-Matches -Actual $svg -Pattern 'Linux_x64_AOT &amp; test NativeAOT size diff'
    Assert-Matches -Actual $svg -Pattern "System\.Private\.CoreLib`n\+129\.0 kB"
    Assert-Matches -Actual $svg -Pattern 'Example\.Library &amp; Tools'
    Assert-Matches -Actual $svg -Pattern "Old\.Library`n-66\.4 kB"
    Assert-Matches -Actual $svg -Pattern 'tabindex="0" role="img"'
    Assert-Matches -Actual $svg -Pattern 'New / Grown'
    Assert-Matches -Actual $svg -Pattern 'Removed / Shrunk'

    Write-Output 'All treemap generator tests passed.'
}
finally {
    Remove-Item -LiteralPath $tempDirectory -Recurse -Force
}
