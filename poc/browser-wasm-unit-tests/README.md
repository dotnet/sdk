# Browser WebAssembly `dotnet test` experiment

This is an experiment, not a supported browser test runner. The
`Microsoft.NET.Sdk.WebAssembly` application contains only C# tests; MSTest
generates its entry point. There are no user-authored MSBuild targets, browser
scripts, or Blazor dependencies.

The experimental `Microsoft.Testing.Platform.Browser` package owns the browser
page, runtime boot module, Playwright launcher, and process cleanup. MTP sends
discovery, output, and results directly to `dotnet test` through managed
`HttpClient`.

The project references `MSTest.TestFramework` and `MSTest.TestAdapter` directly,
at the same version. The broader `MSTest` metapackage would also install report,
coverage, and VSTest SDK packages that this experiment does not use.

Related work: [design RFC](https://github.com/dotnet/sdk/pull/55906) and
[browser package experiment](https://github.com/microsoft/testfx/pull/11305).

## Reproduce using the built SDK and package

Build this SDK checkout with `build.cmd` and pack the testfx browser package.
An installed Chromium-family browser is required; this recipe does not download
one. Run in PowerShell 7 from the SDK repository root. Replace the three inputs
below with the **same package snapshot and version** you intend to test. Do not
repack that snapshot while the run is in progress.

```powershell
$packageSource = (Resolve-Path "C:\path\to\testfx\artifacts\packages\Debug\Shipping" -ErrorAction Stop).Path
$packageVersion = "0.1.0-dev"
$browser = (Resolve-Path "C:\path\to\chromium\chrome.exe" -ErrorAction Stop).Path
$package = Join-Path $packageSource "Microsoft.Testing.Platform.Browser.$packageVersion.nupkg"
$expectedPackageHash = (Get-FileHash $package -Algorithm SHA512 -ErrorAction Stop).Hash
$run = Join-Path ([IO.Path]::GetTempPath()) "browser-wasm-test-$([guid]::NewGuid().ToString('N'))"
$pushed = $false
$useMsbuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$nugetPackages = $env:NUGET_PACKAGES
$project = (Resolve-Path ".\poc\browser-wasm-unit-tests\BrowserWasmTestApp.csproj" -ErrorAction Stop).Path
$dotnet = (Resolve-Path ".\artifacts\bin\redist\Debug\dotnet\dotnet.exe" -ErrorAction Stop).Path
$dotnetRoot = Split-Path $dotnet
$revision = (git rev-parse HEAD).Trim()
$sdks = @(Get-ChildItem "$dotnetRoot\sdk" -Directory | Where-Object {
    $metadata = Join-Path $_.FullName ".toolsetversion"
    (Test-Path $metadata) -and (Get-Content $metadata -First 1) -eq $revision
})
if ($sdks.Count -ne 1) {
    throw "Build this revision first; expected one matching SDK in the Debug redist."
}
$sdkVersion = $sdks[0].Name
$properties = @(
    "-p:DotnetTestInvocation=true"
    "-p:BrowserWasmMtpPackageVersion=$packageVersion"
    "-p:TestingPlatformBrowserExecutable=$browser"
    "-p:RestoreAdditionalProjectSources=$packageSource"
    "-p:UseSharedCompilation=false"
    "-p:MSBuildProjectExtensionsPath=$run\obj\"
    "-p:BaseIntermediateOutputPath=$run\obj\"
    "-p:BaseOutputPath=$run\bin\"
)

try {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"
    $env:NUGET_PACKAGES = "$run\packages"
    New-Item -ItemType Directory -Path $run -ErrorAction Stop | Out-Null
    @{
        sdk = @{
            version = $sdkVersion
            rollForward = "disable"
            paths = @($dotnetRoot)
        }
        test = @{ runner = "Microsoft.Testing.Platform" }
    } | ConvertTo-Json -Depth 3 | Set-Content "$run\global.json" -Encoding utf8 -ErrorAction Stop

    Push-Location $run
    $pushed = $true
    $selectedSdk = & $dotnet --version
    if ($LASTEXITCODE -ne 0 -or $selectedSdk.Trim() -ne $sdkVersion) {
        throw "The built SDK was not selected."
    }
    & $dotnet restore $project @properties
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE."
    }
    $version = $packageVersion.ToLowerInvariant()
    $hashFile = "$env:NUGET_PACKAGES\microsoft.testing.platform.browser\$version\microsoft.testing.platform.browser.$version.nupkg.sha512"
    $actualPackageHash = [Convert]::ToHexString(
        [Convert]::FromBase64String((Get-Content $hashFile -Raw).Trim()))
    if ($actualPackageHash -ne $expectedPackageHash) {
        throw "Restore selected a different browser package than the supplied snapshot."
    }
    & $dotnet test --project $project --no-restore @properties
    if ($LASTEXITCODE -ne 0) {
        throw "Browser tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = $useMsbuildServer
    $env:NUGET_PACKAGES = $nugetPackages
    if ($pushed) {
        Pop-Location
    }
    if (Test-Path -LiteralPath $run) {
        Remove-Item -LiteralPath $run -Recurse -Force -ErrorAction Stop
    }
}
```

Add `--list-tests` to the second command to discover tests without running
them. The explicit restore ensures NuGet has generated the experimental
package imports before `dotnet test` evaluates `ComputeRunArguments`.

The single `DotnetTestInvocation=true` property is an experiment marker
consumed by the browser package. It is supplied explicitly here rather than
added to the shipping SDK.

The SDK's `.toolsetversion` identifies the source revision; older SDK directories
can remain in an incremental layout without being selected by timestamp.
The temporary `global.json` prevents the repository's bootstrap SDK from
silently replacing the selected SDK. `NUGET_PACKAGES` keeps restore and
subsequent package imports on the same cache. The hash check detects same-version
local package mismatches. Build-server reuse and shared compilation are disabled
for this invocation to avoid leaving servers holding files in the disposable
directory.

No application-authored JavaScript or `[JSImport]` is needed. The browser still
requires the package-owned boot module to instantiate the WebAssembly runtime
and call managed `Main(args)`.

## Scope and limitations

- The package wraps the canonical WebAssembly SDK/WasmAppHost command only when
  the caller supplies the experiment marker. It is not a supported SDK extension
  contract and does not support later custom `ComputeRunArguments` providers.
- Test results travel directly from managed MTP to the SDK over authenticated
  loopback HTTP. The package's private browser bindings only carry startup
  arguments and the terminal process-style result.
- `WasmBuildNative=false` and `PublishTrimmed=false` deliberately use the
  prebuilt runtime and untrimmed tests. Successful execution does not establish
  AOT/native-relink or trimming support.
- Browser virtual-file-system reports and attachments are not host files.
  Artifact export, host configuration-file transfer, and graceful managed
  cancellation are not implemented by this sample.
- This local reproduction is not cross-platform CI or a security certification.
  A forcibly terminated shell may leave temporary state; it does not run the
  `finally` block. Do not infer process containment from the recipe's file cleanup.
