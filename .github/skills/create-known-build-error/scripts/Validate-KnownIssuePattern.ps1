<#
.SYNOPSIS
    Validates a Known Build Error pattern (ErrorMessage or ErrorPattern) against sample error text.

.DESCRIPTION
    Tests whether a proposed Known Build Error pattern correctly matches the provided error text,
    using the same matching rules as the Build Analysis system:
      - ErrorMessage: case-insensitive String.Contains, per line
      - ErrorPattern: Regex with Singleline | IgnoreCase flags, per line
      - Multi-line: all entries must match consecutively in order

.PARAMETER ErrorText
    The actual error text to match against (multi-line string).

.PARAMETER ErrorTextFile
    Path to a file containing the error text.

.PARAMETER ErrorMessage
    The proposed ErrorMessage value (String.Contains matching).

.PARAMETER ErrorPattern
    The proposed ErrorPattern value (Regex matching).

.EXAMPLE
    .\Validate-KnownIssuePattern.ps1 -ErrorText "error CS8034: Unable to load Analyzer" -ErrorMessage "Unable to load Analyzer"

.EXAMPLE
    .\Validate-KnownIssuePattern.ps1 -ErrorTextFile errors.txt -ErrorPattern "error CS\d+: Unable to load .*"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ErrorText,

    [Parameter(Mandatory=$false)]
    [string]$ErrorTextFile,

    [Parameter(Mandatory=$false)]
    [string]$ErrorMessage,

    [Parameter(Mandatory=$false)]
    [string]$ErrorPattern
)

$ErrorActionPreference = "Stop"

# Load error text from file if specified
if ($ErrorTextFile) {
    if (-not (Test-Path $ErrorTextFile)) {
        Write-Error "Error text file not found: $ErrorTextFile"
        exit 1
    }
    $ErrorText = Get-Content $ErrorTextFile -Raw
}

if ([string]::IsNullOrWhiteSpace($ErrorText)) {
    Write-Error "Either -ErrorText or -ErrorTextFile is required."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($ErrorMessage) -and [string]::IsNullOrWhiteSpace($ErrorPattern)) {
    Write-Error "Either -ErrorMessage or -ErrorPattern is required."
    exit 1
}

$lines = $ErrorText -split "`n" | ForEach-Object { $_.TrimEnd("`r") }

function Truncate([string]$s, [int]$maxLen) {
    if ($s.Length -le $maxLen) { return $s }
    return $s.Substring(0, $maxLen) + "..."
}

function ParseArrayOrSingle([string]$value) {
    $value = $value.Trim()
    if ($value.StartsWith("[") -and $value.EndsWith("]")) {
        $inner = $value.Substring(1, $value.Length - 2)
        $result = @($inner -split "," | ForEach-Object { $_.Trim().Trim('"').Trim("'") } | Where-Object { $_ -ne "" })
        return ,$result
    }
    return ,@($value)
}

function Test-SingleLineMessage([string]$msg, [string[]]$errorLines) {
    $matches = @()
    for ($i = 0; $i -lt $errorLines.Length; $i++) {
        if ($errorLines[$i].IndexOf($msg, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $matches += [PSCustomObject]@{ LineNum = $i + 1; Line = $errorLines[$i] }
        }
    }
    return $matches
}

function Test-SingleLinePattern([System.Text.RegularExpressions.Regex]$regex, [string[]]$errorLines) {
    $matches = @()
    for ($i = 0; $i -lt $errorLines.Length; $i++) {
        if ($regex.IsMatch($errorLines[$i])) {
            $matches += [PSCustomObject]@{ LineNum = $i + 1; Line = $errorLines[$i] }
        }
    }
    return $matches
}

function Test-MultiLine([string[]]$patterns, [string[]]$errorLines, [bool]$isRegex, [System.Text.RegularExpressions.Regex[]]$regexes) {
    # Multi-line matching: patterns must appear in order, each in a subsequent line
    # Pattern[1] is searched in lines AFTER pattern[0] matched (not necessarily consecutive)
    for ($startLine = 0; $startLine -lt $errorLines.Count; $startLine++) {
        # Check if pattern[0] matches this line
        $firstMatch = $false
        if ($isRegex) {
            $firstMatch = $regexes[0].IsMatch($errorLines[$startLine])
        } else {
            $firstMatch = $errorLines[$startLine].IndexOf($patterns[0], [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        }
        if (-not $firstMatch) { continue }

        # Try to match remaining patterns in subsequent lines
        $allMatch = $true
        $matchedLines = @($startLine)
        $searchFrom = $startLine + 1
        for ($p = 1; $p -lt $patterns.Count; $p++) {
            $found = $false
            for ($line = $searchFrom; $line -lt $errorLines.Count; $line++) {
                $lineMatch = $false
                if ($isRegex) {
                    $lineMatch = $regexes[$p].IsMatch($errorLines[$line])
                } else {
                    $lineMatch = $errorLines[$line].IndexOf($patterns[$p], [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                }
                if ($lineMatch) {
                    $matchedLines += $line
                    $searchFrom = $line + 1
                    $found = $true
                    break
                }
            }
            if (-not $found) { $allMatch = $false; break }
        }

        if ($allMatch) {
            return @{ Found = $true; StartLine = $startLine; MatchedLines = $matchedLines }
        }
    }
    return @{ Found = $false; StartLine = -1; MatchedLines = @() }
}

# ErrorPattern takes priority (same as build-insights)
if (-not [string]::IsNullOrWhiteSpace($ErrorPattern)) {
    $patterns = ParseArrayOrSingle $ErrorPattern
    $modeLabel = if ($patterns.Count -eq 1) { "single-line" } else { "$($patterns.Count)-line" }

    Write-Host "Validating ErrorPattern ($modeLabel)"
    Write-Host "  Pattern(s): $(($patterns | ForEach-Object { "/$_/" }) -join ' -> ')"
    Write-Host "  Matching mode: Regex (Singleline | IgnoreCase)"
    Write-Host "  Error text: $($lines.Length) lines"
    Write-Host ""

    # Compile regexes
    $regexes = @()
    $regexOptions = [System.Text.RegularExpressions.RegexOptions]::Singleline -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    for ($i = 0; $i -lt $patterns.Count; $i++) {
        try {
            $regex = [System.Text.RegularExpressions.Regex]::new($patterns[$i], $regexOptions, [System.TimeSpan]::FromSeconds(5))
            $regexes += $regex
        } catch {
            Write-Host "X INVALID REGEX at pattern [$i]: $($_.Exception.Message)"
            Write-Host "  Pattern: $($patterns[$i])"
            exit 1
        }
    }
    Write-Host "OK Regex compiles successfully"
    Write-Host ""

    if ($patterns.Count -eq 1) {
        $found = @(Test-SingleLinePattern $regexes[0] $lines)
        if ($found.Count -gt 0) {
            Write-Host "MATCH: Regex matched $($found.Count) line(s)"
            $found | Select-Object -First 5 | ForEach-Object {
                Write-Host "  Line $($_.LineNum): $(Truncate $_.Line 200)"
            }
            if ($found.Count -gt 5) { Write-Host "  ... and $($found.Count - 5) more" }

            $matchRate = $found.Count / $lines.Count
            if ($matchRate -gt 0.1 -and $lines.Count -gt 10) {
                Write-Host ""
                Write-Host "WARNING: Pattern matched $([math]::Round($matchRate * 100))% of all lines ($($found.Count)/$($lines.Count))"
                Write-Host "  This pattern may be too generic. Consider making it more specific."
            }
            exit 0
        } else {
            Write-Host "NO MATCH: Regex did not match any line"
            exit 1
        }
    } else {
        $result = Test-MultiLine $patterns $lines $true $regexes
        if ($result.Found) {
            Write-Host "MATCH: All $($patterns.Count) patterns matched in order"
            for ($p = 0; $p -lt $patterns.Count; $p++) {
                $lineIdx = $result.MatchedLines[$p]
                Write-Host "  Line $($lineIdx + 1): $(Truncate $lines[$lineIdx] 200)"
            }
            exit 0
        } else {
            Write-Host "NO MATCH: Could not find all $($patterns.Count) patterns in order"
            for ($p = 0; $p -lt $patterns.Count; $p++) {
                $count = @(Test-SingleLinePattern $regexes[$p] $lines).Count
                $status = if ($count -gt 0) { "matched $count line(s)" } else { "no matches" }
                Write-Host "  Pattern [$p]: $status"
            }
            exit 1
        }
    }
} else {
    $messages = ParseArrayOrSingle $ErrorMessage
    $modeLabel = if ($messages.Count -eq 1) { "single-line" } else { "$($messages.Count)-line" }

    Write-Host "Validating ErrorMessage ($modeLabel)"
    $patternDisplay = ($messages | ForEach-Object { '"' + $_ + '"' }) -join ' -> '
    Write-Host "  Pattern(s): $patternDisplay"
    Write-Host "  Matching mode: String.Contains (case-insensitive)"
    Write-Host "  Error text: $($lines.Length) lines"
    Write-Host ""

    if ($messages.Count -eq 1) {
        $found = @(Test-SingleLineMessage $messages[0] $lines)
        if ($found.Count -gt 0) {
            Write-Host "MATCH: Found $($found.Count) matching line(s)"
            $found | Select-Object -First 5 | ForEach-Object {
                Write-Host "  Line $($_.LineNum): $(Truncate $_.Line 200)"
            }
            if ($found.Count -gt 5) { Write-Host "  ... and $($found.Count - 5) more" }

            $matchRate = $found.Count / $lines.Count
            if ($matchRate -gt 0.1 -and $lines.Count -gt 10) {
                Write-Host ""
                Write-Host "WARNING: Pattern matched $([math]::Round($matchRate * 100))% of all lines ($($found.Count)/$($lines.Count))"
                Write-Host "  This pattern may be too generic. Consider making it more specific."
            }
            exit 0
        } else {
            Write-Host "NO MATCH: ErrorMessage not found in any line"
            # Suggest close matches
            $words = @($messages[0] -split " " | Where-Object { $_.Length -gt 3 })
            if ($words.Count -gt 0) {
                $candidates = @()
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    $matchedWords = @($words | Where-Object { $lines[$i].IndexOf($_, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count
                    if ($matchedWords -ge [math]::Max(1, [math]::Floor($words.Count / 2))) {
                        $candidates += [PSCustomObject]@{ LineNum = $i + 1; Line = $lines[$i]; MatchedWords = $matchedWords }
                    }
                }
                if ($candidates.Count -gt 0) {
                    Write-Host ""
                    Write-Host "Possible close matches (lines containing similar words):"
                    $candidates | Sort-Object MatchedWords -Descending | Select-Object -First 3 | ForEach-Object {
                        Write-Host "  Line $($_.LineNum): $(Truncate $_.Line 200)"
                    }
                }
            }
            exit 1
        }
    } else {
        $result = Test-MultiLine $messages $lines $false @()
        if ($result.Found) {
            Write-Host "MATCH: All $($messages.Count) patterns matched in order"
            for ($p = 0; $p -lt $messages.Count; $p++) {
                $lineIdx = $result.MatchedLines[$p]
                Write-Host "  Line $($lineIdx + 1): $(Truncate $lines[$lineIdx] 200)"
            }
            exit 0
        } else {
            Write-Host "NO MATCH: Could not find all $($messages.Count) patterns in order"
            for ($p = 0; $p -lt $messages.Count; $p++) {
                $count = @(Test-SingleLineMessage $messages[$p] $lines).Count
                $status = if ($count -gt 0) { "matched $count line(s)" } else { "no matches" }
                Write-Host "  Pattern [$p]: $status"
            }
            exit 1
        }
    }
}
