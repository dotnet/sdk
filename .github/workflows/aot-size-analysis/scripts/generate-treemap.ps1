#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InputPath,

    [Parameter(Mandatory)]
    [string] $OutputPath,

    [Parameter(Mandatory)]
    [string] $Platform
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$width = 1440
$height = 900
$panelTop = 140
$panelHeight = 720
$panelWidth = 684
$panelGap = 24

function ConvertTo-Bytes([string] $value, [string] $unit) {
    $multiplier = switch ($unit) {
        'B' { 1 }
        'kB' { 1KB }
        'MB' { 1MB }
        'GB' { 1GB }
        default { throw "Unsupported size unit: $unit" }
    }

    $number = [double]::Parse(
        $value.Replace(',', '.'),
        [Globalization.NumberStyles]::AllowDecimalPoint,
        $invariantCulture)
    return [long][Math]::Round($number * $multiplier)
}

function Format-Bytes([long] $bytes) {
    if ($bytes -lt 1KB) {
        return "$bytes B"
    }
    if ($bytes -lt 1MB) {
        return (($bytes / 1KB).ToString('0.0', $invariantCulture) + ' kB')
    }
    if ($bytes -lt 1GB) {
        return (($bytes / 1MB).ToString('0.0', $invariantCulture) + ' MB')
    }
    return (($bytes / 1GB).ToString('0.00', $invariantCulture) + ' GB')
}

function ConvertTo-Xml([string] $value) {
    return [Security.SecurityElement]::Escape($value)
}

function ConvertTo-SvgNumber([double] $value) {
    return $value.ToString('0.0', $invariantCulture)
}

function Read-Entries([string] $path) {
    $entries = [Collections.Generic.List[object]]::new()
    $section = $null

    foreach ($line in [IO.File]::ReadLines($path)) {
        if ($line -eq '=== New / Grown ===') {
            $section = 'grown'
            continue
        }
        if ($line -eq '=== Removed / Shrunk ===') {
            $section = 'shrunk'
            continue
        }

        if ($section -and $line -match '^\s*([+-])(\d+(?:[.,]\d+)?)\s+(B|kB|MB|GB)\s+(.+?)\s*$') {
            $entries.Add([PSCustomObject]@{
                Kind = if ($Matches[1] -eq '+') { 'grown' } else { 'shrunk' }
                Name = $Matches[4]
                Bytes = ConvertTo-Bytes $Matches[2] $Matches[3]
            })
        }
        elseif ($line -match '^\s*(New|Removed) (frozen objects|blobs): [+-](\d+(?:[.,]\d+)?)\s+(B|kB|MB|GB)\s*$') {
            $entries.Add([PSCustomObject]@{
                Kind = if ($Matches[1] -eq 'New') { 'grown' } else { 'shrunk' }
                Name = [Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($Matches[2])
                Bytes = ConvertTo-Bytes $Matches[3] $Matches[4]
            })
        }
    }

    return $entries | Where-Object Bytes -gt 0 | Sort-Object Bytes -Descending
}

function Write-Panel(
    [Text.StringBuilder] $svg,
    [object[]] $items,
    [string] $title,
    [string] $sign,
    [int] $x,
    [int] $baseHue) {

    $total = [long]($items | Measure-Object Bytes -Sum).Sum
    $escapedTitle = ConvertTo-Xml $title
    $formattedTotal = ConvertTo-Xml "$sign$(Format-Bytes $total)"
    [void] $svg.AppendLine("<rect x=`"$x`" y=`"$panelTop`" width=`"$panelWidth`" height=`"$panelHeight`" rx=`"8`" class=`"panel`"/>")
    [void] $svg.AppendLine("<text x=`"$($x + 16)`" y=`"$($panelTop + 30)`" class=`"panel-title`">$escapedTitle</text>")
    [void] $svg.AppendLine("<text x=`"$($x + $panelWidth - 16)`" y=`"$($panelTop + 30)`" text-anchor=`"end`" class=`"panel-total`">$formattedTotal</text>")

    if ($total -eq 0) {
        return
    }

    $tileTop = $panelTop + 46
    $tileHeight = $panelHeight - 50
    $y = [double]$tileTop
    for ($index = 0; $index -lt $items.Count; $index++) {
        $item = $items[$index]
        $remaining = ($tileTop + $tileHeight) - $y
        $itemHeight = if ($index -eq $items.Count - 1) {
            $remaining
        } else {
            $tileHeight * $item.Bytes / $total
        }

        $hash = [Math]::Abs(($item.Name.ToCharArray() | Measure-Object -Sum { [int]$_ }).Sum)
        $color = "hsl($($baseHue + ($hash % 24)) 62% 40%)"
        $escapedName = ConvertTo-Xml $item.Name
        $formattedSize = ConvertTo-Xml "$sign$(Format-Bytes $item.Bytes)"
        $svgY = ConvertTo-SvgNumber $y
        $svgHeight = ConvertTo-SvgNumber ([Math]::Max(0, $itemHeight - 2))
        [void] $svg.AppendLine("<g tabindex=`"0`" role=`"img`"><title>$escapedName`n$formattedSize</title>")
        [void] $svg.AppendLine("<rect x=`"$($x + 4)`" y=`"$svgY`" width=`"$($panelWidth - 8)`" height=`"$svgHeight`" fill=`"$color`" class=`"tile`"/>")
        if ($itemHeight -ge 34) {
            [void] $svg.AppendLine("<text x=`"$($x + 12)`" y=`"$(ConvertTo-SvgNumber ($y + 19))`" class=`"label`">$escapedName</text>")
            if ($itemHeight -ge 54) {
                [void] $svg.AppendLine("<text x=`"$($x + 12)`" y=`"$(ConvertTo-SvgNumber ($y + 38))`" class=`"size`">$formattedSize</text>")
            }
        }
        [void] $svg.AppendLine('</g>')
        $y += $itemHeight
    }
}

$entries = @(Read-Entries $InputPath)
if ($entries.Count -eq 0) {
    throw "No assembly size changes were found in '$InputPath'."
}

$grown = @($entries | Where-Object Kind -eq 'grown')
$shrunk = @($entries | Where-Object Kind -eq 'shrunk')
$net = ($grown | Measure-Object Bytes -Sum).Sum - ($shrunk | Measure-Object Bytes -Sum).Sum
$netText = if ($net -ge 0) { "+$(Format-Bytes $net)" } else { "-$(Format-Bytes (-$net))" }
$escapedPlatform = ConvertTo-Xml $Platform

$svg = [Text.StringBuilder]::new()
[void] $svg.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void] $svg.AppendLine("<svg xmlns=`"http://www.w3.org/2000/svg`" width=`"$width`" height=`"$height`" viewBox=`"0 0 $width $height`" role=`"img`" aria-labelledby=`"title description`">")
[void] $svg.AppendLine("<title id=`"title`">$escapedPlatform NativeAOT size diff</title>")
[void] $svg.AppendLine("<desc id=`"description`">$netText visualized net change. Rectangle height represents absolute reported size change.</desc>")
[void] $svg.AppendLine(@'
<style>
  text { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; fill: #f0f3f6 }
  .background { fill: #0d1117 }
  .panel { fill: #161b22; stroke: #36404a }
  .heading { font-size: 30px; font-weight: 700 }
  .summary, .panel-total, .size { fill: #a9b1ba }
  .summary { font-size: 15px }
  .panel-title { font-size: 18px; font-weight: 650 }
  .panel-total { font-size: 16px }
  .tile { stroke: #0d1117; stroke-width: 1 }
  g:hover .tile, g:focus .tile { filter: brightness(1.25) }
  .label { font-size: 13px; font-weight: 650 }
  .size { font-size: 12px }
</style>
'@)
[void] $svg.AppendLine("<rect width=`"$width`" height=`"$height`" class=`"background`"/>")
[void] $svg.AppendLine("<text x=`"24`" y=`"48`" class=`"heading`">$escapedPlatform NativeAOT size diff</text>")
[void] $svg.AppendLine("<text x=`"24`" y=`"78`" class=`"summary`">$netText net change | Rectangle height represents absolute reported size change | Hover or focus for details</text>")
Write-Panel $svg $grown 'New / Grown' '+' 24 122
Write-Panel $svg $shrunk 'Removed / Shrunk' '-' (24 + $panelWidth + $panelGap) 0
[void] $svg.AppendLine('</svg>')

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    [IO.Directory]::CreateDirectory($parent) | Out-Null
}
[IO.File]::WriteAllText($OutputPath, $svg.ToString(), [Text.UTF8Encoding]::new($false))
