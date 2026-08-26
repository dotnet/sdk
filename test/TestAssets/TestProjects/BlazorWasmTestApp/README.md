# Blazor WebAssembly test app probe

This test asset exercises the part of
[dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091) that works today:

- the Blazor SDK builds and serves the application;
- MSTest and Microsoft.Testing.Platform run inside `browser-wasm`; and
- the page displays the test application's exit code.

Run the app and open the printed URL in a browser:

```powershell
dotnet run --project test\TestAssets\TestProjects\BlazorWasmTestApp\BlazorWasmTestApp.csproj
```

The single test starts automatically and the page changes to `Passed`. The button can run it again.
This requires the SDK, runtime packs, and flowed Blazor packages to come from a compatible
product build; a repository bootstrap SDK can temporarily be on an older preview band than
the packages in `eng\Version.Details.props`.

## Why `dotnet test` does not work yet

The `MSTest` package marks this project as both `IsTestProject` and
`IsTestingPlatformApplication`. The current SDK consequently treats the command returned by
`ComputeRunArguments` as the test host. For a Blazor WebAssembly project that command is the
host-side Blazor Gateway:

```text
dotnet blazor-gateway.dll --staticWebAssets <manifest>
    @<owner-only-http-bootstrap.rsp>
```

The SDK hosts that authenticated endpoint and routes its existing binary protocol frames to
the normal `dotnet test` handlers. The Gateway serves the application, but it does not yet
consume the owner-only response file, launch a browser, or provide the bootstrap values to
Microsoft.Testing.Platform inside the page. The token is deliberately kept out of process
arguments and URLs.

The remaining integration needs a host-side bridge that:

1. starts the Blazor server and waits for its URL;
2. launches a selected browser, including a headless mode;
3. injects the HTTP endpoint and token into the page without putting the token in a URL;
4. starts Microsoft.Testing.Platform with those bootstrap options;
5. closes the browser and server when the test session completes; and
6. provides a browser-compatible reverse cancellation path.

Per-test results and output can flow live through the HTTP transport. A physical TRX still
needs an explicit export path because a browser's virtual file system disappears with the page.

The reusable pieces already exist, but are not connected for Blazor:

- [`dotnet test` device orchestration](../../../../documentation/specs/dotnet-run-for-maui.md)
  supplies `ComputeAvailableDevices`, `DeployToDevice`, and `ComputeRunArguments`.
- [TestFX BrowserPlayground](https://github.com/microsoft/testfx/tree/main/samples/BrowserPlayground)
  proves Microsoft.Testing.Platform and MSTest can run on `browser-wasm`.
- [dotnet/sdk#55389](https://github.com/dotnet/sdk/pull/55389) is draft foundational work
  for standalone WASM test hosts, but does not implement the Blazor server/browser lifecycle.
