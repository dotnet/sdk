param(
    [Parameter(Mandatory)]
    [string] $BeforeCsv,

    [Parameter(Mandatory)]
    [string] $AfterCsv
)

$ErrorActionPreference = "Stop"
$culture = [Globalization.CultureInfo]::InvariantCulture

function Get-Median([double[]] $Values)
{
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0)
    {
        throw "Cannot calculate a median from an empty sequence."
    }

    $middle = [Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1)
    {
        return $sorted[$middle]
    }

    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

function Read-Results([string] $Path)
{
    if (-not (Test-Path $Path))
    {
        throw "Results file was not found: '$Path'."
    }

    $rows = @(Import-Csv $Path | Where-Object Phase -eq "Measured")
    if ($rows.Count -ne 12)
    {
        throw "Expected 12 measured rows in '$Path', found $($rows.Count)."
    }

    $benchmarks = @($rows.Benchmark | Sort-Object -Unique)
    if ($benchmarks.Count -ne 1)
    {
        throw "Expected one benchmark in '$Path', found: $($benchmarks -join ', ')."
    }

    return [pscustomobject]@{
        Benchmark = $benchmarks[0]
        Label = (@($rows.Label | Sort-Object -Unique) -join ",")
        Rows = $rows
    }
}

$before = Read-Results $BeforeCsv
$after = Read-Results $AfterCsv
if ($before.Benchmark -ne $after.Benchmark)
{
    throw "Cannot compare '$($before.Benchmark)' with '$($after.Benchmark)'."
}

foreach ($metric in @("TotalDurationSeconds", "PreSubmissionDurationSeconds"))
{
    $beforeMedian = Get-Median @(
        $before.Rows | ForEach-Object { [double]::Parse($_.$metric, $culture) })
    $afterMedian = Get-Median @(
        $after.Rows | ForEach-Object { [double]::Parse($_.$metric, $culture) })
    $reduction = $beforeMedian - $afterMedian

    [pscustomobject]@{
        Benchmark = $before.Benchmark
        Metric = $metric
        BeforeLabel = $before.Label
        AfterLabel = $after.Label
        BeforeMedianSeconds = [Math]::Round($beforeMedian, 5)
        AfterMedianSeconds = [Math]::Round($afterMedian, 5)
        ReductionSeconds = [Math]::Round($reduction, 5)
        ReductionPercent = [Math]::Round(($reduction / $beforeMedian) * 100, 2)
    }
}
