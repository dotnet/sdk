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

The launcher evaluates the project's original `RunCommand`/`RunArguments` with
the PoC override disabled and starts the unchanged packaged
`blazor-gateway.dll` directly. A small sidecar layer:

- chooses a loopback port and retries startup races;
- polls the served app instead of parsing `Now listening on:` output;
- writes and validates a versioned launch-info JSON file; and
- disables Gateway HTTPS redirection, HSTS, and telemetry for this local run.

This validates the proposed ownership boundary without modifying Gateway:
Gateway still only serves Static Web Assets, while the launcher owns test
orchestration and readiness.

Test discovery uses the same browser bridge:

```powershell
npm run list:dotnet
```

The earlier `npm test` command remains useful as a control experiment: it runs
the same test in the browser without the `dotnet test` protocol connection.
