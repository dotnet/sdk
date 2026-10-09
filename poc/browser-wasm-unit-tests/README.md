# Browser WebAssembly `dotnet test` experiment

This experiment is a pure-managed `Microsoft.NET.Sdk.WebAssembly` MSTest
application. It contains only C# tests.

The experimental `Microsoft.Testing.Platform.Browser` package owns the browser
page, runtime boot module, Playwright launcher, and process cleanup. MTP sends
discovery, output, and results directly to `dotnet test` through managed
`HttpClient`.

Build the browser package experiment, then run from the repository root:

```powershell
$packageSource = "Q:\src\copilot-worktrees\testfx\dev-amauryleve-animated-umbrella\artifacts\packages\Debug\Shipping"
$browser = "$env:LOCALAPPDATA\ms-playwright\chromium-1234\chrome-win64\chrome.exe"
$run = Join-Path ([IO.Path]::GetTempPath()) "browser-wasm-test-$([guid]::NewGuid().ToString('N'))"
$pushed = $false
$useMsbuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$nugetPackages = $env:NUGET_PACKAGES
$project = (Resolve-Path ".\poc\browser-wasm-unit-tests\BrowserWasmTestApp.csproj").Path
$dotnet = (Resolve-Path ".\artifacts\bin\redist\Debug\dotnet\dotnet.exe").Path
$dotnetRoot = Split-Path $dotnet
$sdkVersions = @(Get-ChildItem "$dotnetRoot\sdk" -Directory | ForEach-Object Name)
if ($sdkVersions.Count -ne 1) {
    throw "Expected exactly one SDK in the Debug redist."
}
$sdkVersion = $sdkVersions[0]
$properties = @(
    "-p:DotnetTestInvocation=true"
    "-p:BrowserWasmMtpPackageVersion=0.1.0-dev"
    "-p:TestingPlatformBrowserExecutable=$browser"
    "-p:RestoreAdditionalProjectSources=$packageSource"
    "-p:MSBuildProjectExtensionsPath=$run\obj\"
    "-p:BaseIntermediateOutputPath=$run\obj\"
    "-p:BaseOutputPath=$run\bin\"
)

try {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"
    $env:NUGET_PACKAGES = "$run\packages"
    New-Item -ItemType Directory -Path $run | Out-Null
    @{
        sdk = @{
            version = $sdkVersion
            rollForward = "disable"
            paths = @($dotnetRoot)
        }
        test = @{ runner = "Microsoft.Testing.Platform" }
    } | ConvertTo-Json -Depth 3 | Set-Content "$run\global.json"

    Push-Location $run
    $pushed = $true
    & $dotnet restore $project @properties
    & $dotnet test --project $project --no-restore @properties
}
finally {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = $useMsbuildServer
    $env:NUGET_PACKAGES = $nugetPackages
    if ($pushed) {
        Pop-Location
    }
    Remove-Item -LiteralPath $run -Recurse -Force -ErrorAction SilentlyContinue
}
```

Add `--list-tests` to the second command to discover tests without running
them. The explicit restore ensures NuGet has generated the experimental
package imports before `dotnet test` evaluates `ComputeRunArguments`.

The single `DotnetTestInvocation=true` property is an experiment marker
consumed by the browser package. It is supplied explicitly here rather than
added to the shipping SDK.

MSBuild server reuse is disabled because the initial restore creates the
experimental package imports that the following `dotnet test` evaluation must
observe.

No application-authored JavaScript or `[JSImport]` is needed. The browser still
requires the package-owned boot module to instantiate the WebAssembly runtime
and call managed `Main(args)`.
