$currentLocation = Get-Location

try {
    Write-Output "builing release"
    Invoke-Expression 'eng\common\build.ps1 -restore'
    Invoke-Expression 'eng\common\build.ps1 -build -configuration release'

    Write-Output "running tests"
    Invoke-Expression 'cd artifacts\bin\dotnet-format.Performance\Release\netcoreapp2.1'
    Invoke-Expression 'dotnet benchmark dotnet-format.Performance.dll --memory --join --filter *'
}
catch {
    exit -1
}
finally {
    Set-Location $currentLocation
}