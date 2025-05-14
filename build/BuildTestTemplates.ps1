<#
.SYNOPSIS
    Builds a list of project files in the dogfood environment.
.DESCRIPTION
    This script enters the dogfood environment and builds the specified project files.
#>
[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Release"
)

function Build-Projects {
    $ErrorActionPreference = 'Stop'
    $RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

    # Define constants for common path segments
    $TemplateFeedPath = "template_feed\Microsoft.DotNet.Common.ProjectTemplates.10.0\content"
    $MSTestPath = "$TemplateFeedPath\MSTest"
    $NUnitPath = "$TemplateFeedPath\NUnit"
    $XUnitPath = "$TemplateFeedPath\XUnit"
    $PlaywrightMSTestPath = "$TemplateFeedPath\Playwright-MSTest"
    $PlaywrightNUnitPath = "$TemplateFeedPath\Playwright-NUnit"

    # Define the project files using the constants
    $ProjectFiles = @(
        # MSTest Projects
        "$MSTestPath-CSharp\Company.TestProject1.csproj",
        "$MSTestPath-FSharp\Company.TestProject1.fsproj",
        "$MSTestPath-VisualBasic\Company.TestProject1.vbproj",
        "$PlaywrightMSTestPath-CSharp\Company.TestProject1.csproj",

        # NUnit Projects
        "$NUnitPath-CSharp\Company.TestProject1.csproj",
        "$NUnitPath-FSharp\Company.TestProject1.fsproj",
        "$NUnitPath-VisualBasic\Company.TestProject1.vbproj",
        "$PlaywrightNUnitPath-CSharp\Company.TestProject1.csproj",

        # XUnit Projects
        "$XUnitPath-CSharp\Company.TestProject1.csproj",
        "$XUnitPath-FSharp\Company.TestProject1.fsproj",
        "$XUnitPath-VisualBasic\Company.TestProject1.vbproj"
    )

    Write-Host "Building projects in the dogfood environment..." -ForegroundColor Cyan

    # Call dogfood.ps1 directly instead of through dogfood.cmd to avoid the -NoExit parameter
    $dogfoodPs1 = Join-Path $RepoRoot "eng\dogfood.ps1"

    $failedProjects = @()

    foreach ($projectFile in $ProjectFiles) {
        $fullProjectPath = Join-Path $RepoRoot $projectFile

        # Check if the project file exists
        if (-not (Test-Path $fullProjectPath)) {
            Write-Error "Project file not found at: $fullProjectPath"
            $failedProjects += $fullProjectPath
            continue
        }

        Write-Host "Executing: dotnet build $fullProjectPath via dogfood environment" -ForegroundColor Gray
        # Pass the command directly to the dogfood.ps1 script
        & $dogfoodPs1 -configuration $configuration -command "dotnet", "build", $fullProjectPath
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            Write-Error "Build failed for project: $fullProjectPath with exit code: $exitCode"
            $failedProjects += $fullProjectPath
        } else {
            Write-Host "Build completed successfully for project: $fullProjectPath" -ForegroundColor Green
        }
    }

    if ($failedProjects.Count -gt 0) {
        Write-Error "The following projects failed to build:"
        $failedProjects | ForEach-Object { Write-Error $_ }
        return 1
    } else {
        Write-Host "All projects built successfully!" -ForegroundColor Green
        return 0
    }
}

# Execute the function using Invoke-Command
$exitCode = Invoke-Command -ScriptBlock ${function:Build-Projects}
exit $exitCode
