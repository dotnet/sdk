# Blazor WebAssembly Static Web Asset (SWA) Baseline Generation

The Blazor WASM SWA baselines are used to determine if the expected assets in the manifest are present, and if new assets have been added. Occasionally, it's required to update the SWA baselines to account for expected changes in the packaged files.

**Please note:** These steps must be performed in a Windows development environment otherwise the line-ending and ordering result in very large diff-counts.

## Updating Baselines

1. Clone SDK repo: `git clone git@github.com:dotnet/sdk.git`
    - Switch branches as appropriate
2. Restore repo: `.\restore.cmd`
3. Build dotnet: `.\build.cmd`
4. Activate local dotnet environment: `.\eng\dogfood.cmd`
5. Enter the following into the CLI from the activated dogfood environment to open VS: `.\src\BlazorWasmSdk\BlazorWasmSdk.slnf`
6. Open `src\Tests\Microsoft.NET.Sdk.Razor.Tests\AspNetSdkBaselineTest.cs`
7. Add `#define GENERATE_SWA_BASELINES` on the first line (above the license comment)
8. Run all tests from `Microsoft.NET.Sdk.BlazorWebAssembly.Tests.csproj`
9. Delete `#define GENERATE_SWA_BASELINES` from step 7.
10. Commit the changes to the project baselines.
