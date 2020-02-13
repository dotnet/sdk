[CmdletBinding(PositionalBinding=$false)]
Param(
    [switch] $micro,
    [switch] $real,
    [switch] $all,
    [switch] $help
  )
  
function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -micro      Run micro-benchmarks (default)"
    Write-Host "  -real       Run real-world benchmarks"
    Write-Host "  -all        Run all benchmarks"
    Write-Host "  -help       Print help and exit"
    Write-Host ""
}

$currentLocation = Get-Location
try {
    
    if ($help) {
        Print-Usage
        exit 0
    }
    
    if (!$micro -and !$real -and !$all) {
        $micro = $true
    }
    
    Write-Output "builing release"
    Invoke-Expression 'eng\common\build.ps1 -restore'
    Invoke-Expression 'eng\common\build.ps1 -build -configuration release'

    Write-Output "running tests"
    Invoke-Expression 'dotnet tool restore '
    
    if ($real -or $all) {
        # Real-World case, download the project
        if (-not (Test-Path -LiteralPath "temp\project-system\ProjectSystem.sln")) {
            Invoke-Expression 'eng\format-verifier.ps1 -repo https://github.com/dotnet/project-system -sha fc3b12e47adaad6e4813dc600acf190156fecc24 -testPath temp -stage prepare'
        }
    }
    
    Invoke-Expression 'cd artifacts\bin\dotnet-format.Performance\Release\netcoreapp2.1'
    
    if ($micro) {
        # Default case, run very small tests
        Invoke-Expression 'dotnet benchmark dotnet-format.Performance.dll --memory --join --filter *Formatted*'
        exit 0
    }
    
    if ($real) {
        Invoke-Expression 'dotnet benchmark dotnet-format.Performance.dll --memory --join --filter RealWorldSolution'
        exit 0
    }
    
    if ($all) {
        Invoke-Expression 'dotnet benchmark dotnet-format.Performance.dll --memory --join --filter *'
        exit 0
    }
}
catch {
    exit -1
}
finally {
    Set-Location $currentLocation
}