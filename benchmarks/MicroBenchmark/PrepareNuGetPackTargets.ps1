param(
    [Parameter(Mandatory)]
    [string] $DotNetRoot,

    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue))
{
    throw "The GitHub CLI ('gh') is required to download the NuGet #7603 patch."
}

if (-not (Get-Command git -ErrorAction SilentlyContinue))
{
    throw "Git is required to apply the NuGet #7603 patch."
}

$dotnetExecutable = Join-Path $DotNetRoot $(if ($IsWindows) { "dotnet.exe" } else { "dotnet" })
if (-not (Test-Path $dotnetExecutable))
{
    throw "dotnet was not found at '$dotnetExecutable'."
}

$sdkVersion = (& $dotnetExecutable --version).Trim()
$sdkDirectory = Join-Path $DotNetRoot "sdk\$sdkVersion"
$packTargetsPath = Join-Path $sdkDirectory "NuGet.Build.Tasks.Pack.targets"
$packTaskAssemblyPath = Join-Path $sdkDirectory "NuGet.Build.Tasks.Pack.dll"
foreach ($path in @($packTargetsPath, $packTaskAssemblyPath))
{
    if (-not (Test-Path $path))
    {
        throw "Required SDK file was not found: '$path'."
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$baselineOutputPath = Join-Path $OutputDirectory "Pack.baseline.targets"
$modifiedOutputPath = Join-Path $OutputDirectory "Pack.modified.targets"
$overridePropsPath = Join-Path $OutputDirectory "override.props"
Copy-Item $packTargetsPath $baselineOutputPath -Force

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "nuget-pack-7603-$([Guid]::NewGuid().ToString('N'))"
try
{
    $repositoryRelativePath = "src/NuGet.Core/NuGet.Build.Tasks/NuGet.Build.Tasks.Pack.targets"
    $temporaryTargetPath = Join-Path $temporaryRoot $repositoryRelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path $temporaryTargetPath) | Out-Null
    Copy-Item $packTargetsPath $temporaryTargetPath

    $commit = gh api "repos/NuGet/NuGet.Client/commits/78cb434a446e09b3fecb89fa0acebd6e0b42c5bb" |
        ConvertFrom-Json
    $targetChange = $commit.files |
        Where-Object filename -eq $repositoryRelativePath |
        Select-Object -First 1
    if ($null -eq $targetChange -or [string]::IsNullOrWhiteSpace($targetChange.patch))
    {
        throw "NuGet #7603 did not contain the expected Pack targets patch."
    }

    $patchPath = Join-Path $temporaryRoot "nuget-7603.patch"
    $patch =
        "diff --git a/$repositoryRelativePath b/$repositoryRelativePath`n" +
        "--- a/$repositoryRelativePath`n" +
        "+++ b/$repositoryRelativePath`n" +
        "$($targetChange.patch)`n"
    [IO.File]::WriteAllText($patchPath, $patch, [Text.UTF8Encoding]::new($false))

    git -C $temporaryRoot apply --ignore-space-change --ignore-whitespace $patchPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "The NuGet #7603 patch did not apply to '$packTargetsPath'."
    }

    Copy-Item $temporaryTargetPath $modifiedOutputPath -Force
}
finally
{
    Remove-Item $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$escapedPackTaskAssemblyPath = [Security.SecurityElement]::Escape($packTaskAssemblyPath)
$overrideProps = @"
<Project>
  <PropertyGroup>
    <ImportNuGetBuildTasksPackTargetsFromSdk>true</ImportNuGetBuildTasksPackTargetsFromSdk>
    <NuGetPackTaskAssemblyFile>$escapedPackTaskAssemblyPath</NuGetPackTaskAssemblyFile>
  </PropertyGroup>
</Project>
"@
[IO.File]::WriteAllText($overridePropsPath, $overrideProps, [Text.UTF8Encoding]::new($false))

Get-FileHash $baselineOutputPath, $modifiedOutputPath -Algorithm SHA256
Write-Host "Override props: $overridePropsPath"
