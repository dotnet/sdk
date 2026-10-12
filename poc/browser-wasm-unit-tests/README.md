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

## Continuous compatibility gate

The public SDK pipeline includes Windows/Linux browser integration jobs that run
[`eng/browser-testing/RunBrowserTests.ps1`](../../eng/browser-testing/RunBrowserTests.ps1).
The same entry point can be run locally in PowerShell 7 with
`-BrowserExecutable <installed Chromium-family browser>`. It builds both repositories,
checks the shipping SDK source stamp and eight package source commits, and requires
every TestFX browser acceptance case to execute successfully. Missing prerequisites,
zero results, skipped cases and malformed result files are failures, not green skips.
The runner passes the exact source-matched SDK version to acceptance, so older SDKs
can coexist in an incremental layout. Each fixture uses an isolated NuGet cache and
checks all eight restored package hashes against the staged package bytes.
SDK compilation uses the existing product-only
[`source-build.slnf`](../../source-build.slnf), including the shipping redist and its
late-published tools without unrelated SDK test projects. This selects projects;
it does not enable source-build mode. It preserves the real SDK-under-test while
reducing the disk space needed to compose both repositories.
TestFX owns the eight-package selection in `BrowserTestingPackages.slnf`; the
shared runner invokes its normal pack build, then builds only the acceptance
runner's evaluated current target framework. On Windows, the package build uses
the pinned bootstrap SDK's MSBuild rather than an independently installed Visual Studio.

The SDK counterpart is pinned in
[`eng/browser-testing/testfx.json`](../../eng/browser-testing/testfx.json).
Cross-repository changes must update the counterpart pins deliberately and run both
compositions before merging; do not replace them with floating branches or approve a
dependency update based only on repository-local tests. The change author owns the
two-repository compatibility fix and evidence through dependency flow. Public release
still requires maintainers to assign the ongoing Playwright/Node/browser servicing owner.

The WasmAppHost `App url:` readiness line is currently an explicit compatibility
dependency. Real browser startup in the required gate exercises that dependency
against the runtime pack selected by the SDK build. Changing the readiness format
requires a coordinated launcher change; a structured runtime-owned readiness API
must not be claimed until it exists upstream.

## Reproduce using the built SDK and package

Build this SDK checkout with `build.cmd` and pack the aligned testfx packages.
An installed Chromium-family browser is required; this recipe does not download
one. Run in PowerShell 7 from the SDK repository root. The package feed must contain
the browser package and matching MSTest, MTP, MTP.MSBuild, telemetry, and TRX
abstractions packages, with each package's source revision and hash recorded in
the feed's `manifest.json`.
Replace the inputs below with that **immutable aligned snapshot**. Do not repack
it while a run is in progress.

```powershell
$packageSource = (Resolve-Path "C:\path\to\testfx\artifacts\packages\Debug\Shipping" -ErrorAction Stop).Path
$packageVersion = "0.1.0-dev"
$mstestVersion = "4.6.0-dev"
$browser = (Resolve-Path "C:\path\to\chromium\chrome.exe" -ErrorAction Stop).Path
$manifest = Get-Content (Join-Path $packageSource "manifest.json") -Raw -ErrorAction Stop | ConvertFrom-Json
foreach ($entry in $manifest) {
    if ((Get-FileHash (Join-Path $packageSource $entry.Name) -Algorithm SHA256 -ErrorAction Stop).Hash -ne $entry.SHA256) {
        throw "The package snapshot does not match its manifest: $($entry.Name)"
    }
}
$results = Join-Path (Get-Location) "TestResults\browser-wasm"
$run = Join-Path ([IO.Path]::GetTempPath()) "browser-wasm-test-$([guid]::NewGuid().ToString('N'))"
$pushed = $false
$useMsbuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$disableNodeReuse = $env:MSBUILDDISABLENODEREUSE
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
    "-p:BrowserWasmMtpPackageVersion=$packageVersion"
    "-p:BrowserWasmMSTestVersion=$mstestVersion"
    "-p:TestingPlatformBrowserExecutable=$browser"
    "-p:RestoreAdditionalProjectSources=$packageSource"
    "-p:UseSharedCompilation=false"
    "-p:MSBuildProjectExtensionsPath=$run\obj\"
    "-p:BaseIntermediateOutputPath=$run\obj\"
    "-p:BaseOutputPath=$run\bin\"
)

try {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"
    $env:MSBUILDDISABLENODEREUSE = "1"
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
    $assets = Get-Content "$run\obj\project.assets.json" -Raw -ErrorAction Stop | ConvertFrom-Json
    foreach ($entry in $manifest) {
        $library = @($assets.libraries.PSObject.Properties | Where-Object {
            ($_.Name.Replace("/", ".") + ".nupkg") -eq $entry.Name
        })
        if ($library.Count -ne 1) {
            throw "The aligned package was not restored: $($entry.Name)"
        }
        $actualHash = [Convert]::ToHexString([Convert]::FromBase64String($library[0].Value.sha512))
        if ($actualHash -ne (Get-FileHash (Join-Path $packageSource $entry.Name) -Algorithm SHA512 -ErrorAction Stop).Hash) {
            throw "Restore selected a different package than the supplied snapshot: $($entry.Name)"
        }
    }
    & $dotnet test --project $project --no-restore --results-directory $results @properties
    if ($LASTEXITCODE -ne 0) {
        throw "Browser tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = $useMsbuildServer
    $env:MSBUILDDISABLENODEREUSE = $disableNodeReuse
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

No invocation marker is required. The SDK completes `ComputeRunArguments` and
its providers, then invokes `ComputeTestingPlatformRunArguments` and consumes the
separate `TestingPlatformRunCommand`, `TestingPlatformRunArguments`, and
`TestingPlatformRunWorkingDirectory` outputs. The package rejects SDKs lacking
`TestingPlatformRunArgumentsVersion=1`; ordinary `dotnet run` is unchanged.

The sample also exports `browser-runtime.txt` through `TestContext.AddResultFile`.
The SDK commits it beneath `TestResults\browser-wasm`, isolated from other modules.
These result files are retained; only the disposable restore/build directory is
deleted by the recipe.

The SDK's `.toolsetversion` identifies the source revision; older SDK directories
can remain in an incremental layout without being selected by timestamp.
The temporary `global.json` prevents the repository's bootstrap SDK from
silently replacing the selected SDK. `NUGET_PACKAGES` keeps restore and
subsequent package imports on the same cache. The hash check detects same-version
local package mismatches. Build-server reuse, MSBuild worker-node reuse, and
shared compilation are disabled for this invocation to avoid leaving servers
holding files in the disposable directory.

No application-authored JavaScript or `[JSImport]` is needed. The browser still
requires the package-owned boot module to instantiate the WebAssembly runtime
and call managed `Main(args)`.

## Scope and limitations

- The package wraps the canonical WebAssembly SDK/WasmAppHost command through
  the SDK's test-only selection target, after ordinary run-argument providers.
- Test results travel directly from managed MTP to the SDK over authenticated
  loopback HTTP. The package's private browser bindings only carry startup
  arguments and the terminal process-style result.
- `WasmBuildNative=false` and `PublishTrimmed=false` deliberately use the
  prebuilt runtime and untrimmed tests. Successful execution does not establish
  AOT/native-relink or trimming support.
- Remote artifacts are streamed in chunks of at most 1 MiB. Defaults are 1 GiB
  per file, 4 GiB and 10,000 files per module. Positive integer overrides are
  `DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_BYTES`,
  `DOTNET_CLI_TEST_ARTIFACT_MAX_MODULE_BYTES`, and
  `DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_COUNT`. Incomplete or rejected transfers
  cannot turn into successful host artifact reports.
- Cancellation uses the negotiated MTP HTTP control channel; results and final
  artifacts can finish before shutdown. A second Ctrl+C still forces exit.
  Host configuration-file transfer and browser-side artifact merging are not
  provided; exported originals remain available to host tooling.
- Official signing, SDK dependency flow, and the supported release/RID matrix
  still require release validation. A local signing dry run is not official
  signing or proof of cross-platform execution.
- This local reproduction is not cross-platform CI or a security certification.
  A forcibly terminated shell may leave temporary state; it does not run the
  `finally` block. Do not infer process containment from the recipe's file cleanup.
