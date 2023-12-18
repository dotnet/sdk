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
    Invoke-Expression 'eng\common\build.ps1 -restore -msbuildEngine dotnet'
    Invoke-Expression 'eng\common\build.ps1 -build  -msbuildEngine dotnet -configuration release'

    Write-Output "running tests"
    
    if ($real -or $all) {
        # Real-World case, download the project
        if (-not (Test-Path -LiteralPath "temp\project-system\ProjectSystem.sln")) {
            Invoke-Expression 'eng\format-verifier.ps1 -repo https://github.com/dotnet/project-system -sha 88387e5b7f3c9ccd342562a157e67f4a639ef421 -testPath temp -stage prepare'
            Invoke-Expression 'dotnet restore temp\project-system\ProjectSystem.sln'
        }
    }
    
    Invoke-Expression 'cd perf'
    
    if ($micro) {
        # Default case, run very small tests
        Invoke-Expression 'dotnet run -c Release -f net9.0 --runtimes net9.0 --project dotnet-format.Performance.csproj -- --memory --join --filter Microsoft.CodeAnalysis.Tools.Perf.Micro*'
        exit 0
    }
    
    if ($real) {
        Invoke-Expression 'dotnet run -c Release -f net9.0 --runtimes net9.0 --project dotnet-format.Performance.csproj -- --memory --join --filter Microsoft.CodeAnalysis.Tools.Perf.Real*'
        exit 0
    }
    
    if ($all) {
        Invoke-Expression 'dotnet run -c Release -f net9.0 --runtimes net9.0 --project dotnet-format.Performance.csproj -- --memory --join --filter *'
        exit 0
    }
}
catch {
    exit -1
}
finally {
    Set-Location $currentLocation
}