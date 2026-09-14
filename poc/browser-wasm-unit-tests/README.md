# Browser unit-test PoC

This local proof of concept:

1. builds `BlazorWasmTestApp`;
2. starts the existing Blazor Gateway on an ephemeral loopback port;
3. launches a Playwright-managed headless Chromium instance;
4. waits for the page's MSTest run to report `Passed`; and
5. closes the browser and server.

From this directory:

```powershell
npm install
npx playwright install chromium
npm test
```

After the first successful build, skip rebuilding with:

```powershell
$env:BROWSER_WASM_POC_SKIP_BUILD = "1"
npm test
```

The runner prefers the repository's `.dotnet\dotnet.exe` when it exists and
otherwise uses the SDK selected by `global.json`.

## `dotnet test` integration

The Blazor test asset conditionally references the locally built
`Microsoft.Testing.Platform.Browser` package. Configure the local package
source and a Chromium executable:

```powershell
$env:MTP_BROWSER_PACKAGE_SOURCE = "Q:\src\copilot-worktrees\testfx\dev-amauryleve-animated-umbrella\artifacts\packages\Release\Shipping"
$env:MTP_BROWSER_EXECUTABLE = "$env:LOCALAPPDATA\ms-playwright\chromium-1234\chrome-win64\chrome.exe"
npm run test:dotnet
```

This path uses the SDK's authenticated HTTP `dotnettestcli` gateway. The
package's launcher starts the existing Gateway host, expands the owner-only
response file in memory, injects the arguments before Blazor starts, and lets
MTP stream the test results directly back to `dotnet test`.

The launcher wraps the host command produced by the project's normal
`ComputeRunArguments` target and starts the unchanged packaged
`blazor-gateway.dll`. Gateway still only serves Static Web Assets, while the
browser package owns test orchestration. The package accepts a versioned
launch-info file from hosts that implement the generic readiness contract and
temporarily recognizes the current ASP.NET Core listening message as a
compatibility fallback.

Because Blazor owns this application's page, it disables the package's default
host assets and composes through the package's versioned page API:
`testingPlatformBrowser.contractVersion`, `getArguments()`, and
`complete(exitCode)`.

Test discovery uses the same browser bridge:

```powershell
npm run list:dotnet
```

Filtering also uses the normal `dotnet test` option:

```powershell
npm run filter:dotnet
```

The earlier `npm test` command remains useful as a control experiment: it runs
the same test in the browser without the `dotnet test` protocol connection.
