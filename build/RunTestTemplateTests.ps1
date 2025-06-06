<#
.SYNOPSIS
    Runs Microsoft.TestTemplates.Acceptance.Tests.dll in the dogfood environment.
.DESCRIPTION
    This script enters the dogfood environment and runs the RunTestTemplateTests tests.
#>
[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Release"
)

function Run-TestTemplateTests {
    $ErrorActionPreference = 'Stop'
    $RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    $classNameFilter = "--filter"
	$filterValue = "FullyQualifiedName~Microsoft.DotNet.Cli.New.IntegrationTests.DotnetNewTestTemplatesTests"
    $TestDll = Join-Path $RepoRoot "artifacts\bin\dotnet-new.IntegrationTests\$configuration\dotnet-new.IntegrationTests.dll"

    # Check if the test DLL exists
    if (-not (Test-Path $TestDll)) {
        Write-Error "Test DLL not found at: $TestDll"
        return 1
    }

    Write-Host "Running tests for test templates in the dogfood environment..." -ForegroundColor Cyan

    # Call dogfood.ps1 directly instead of through dogfood.cmd to avoid the -NoExit parameter
    $dogfoodPs1 = Join-Path $RepoRoot "eng\dogfood.ps1"

    Write-Host "Executing: dotnet test $TestDll via dogfood environment" -ForegroundColor Gray
    # Pass the command directly to the dogfood.ps1 script
	& $dogfoodPs1 -configuration $configuration -command @("dotnet", "test", $TestDll, $classNameFilter, $filterValue)

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Error "Tests failed with exit code: $exitCode"
    } else {
        Write-Host "Tests completed successfully!" -ForegroundColor Green
    }

    return $exitCode
}

# Execute the function using Invoke-Command
$exitCode = Invoke-Command -ScriptBlock ${function:Run-TestTemplateTests}
exit $exitCode
