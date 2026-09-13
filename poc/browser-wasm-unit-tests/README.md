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

The PoC intentionally does not integrate with `dotnet test` yet. It proves the
core execution fact: MSTest and Microsoft.Testing.Platform can execute a test
assembly inside a real `browser-wasm` runtime.
