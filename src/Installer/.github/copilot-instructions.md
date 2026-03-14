
---
applyTo: "**"
---
- Environment: Windows 11 using PowerShell 7
- Never use `&&` to chain commands; use semicolon (`;`) for PowerShell command chaining
- Prefer PowerShell cmdlets over external utilities when available
- Use PowerShell-style parameter syntax (-Parameter) rather than Unix-style flags

Code Style:
- An `.editorconfig` at `src/Installer/.editorconfig` governs all dotnetup code. Follow it strictly for new code.
- Key conventions: file-scoped namespaces, `s_` prefix for static fields, `_` prefix for instance fields, file headers, sorted usings, `ConfigureAwait(false)`, `CultureInfo.InvariantCulture` for formatting.
- All projects use `TreatWarningsAsErrors`. Style violations break the build.
- To auto-format a project: `d:\sdk\.dotnet\dotnet format <project.csproj> --no-restore`
- When debugging or iterating on a bug fix, it is fine to temporarily ignore style issues and fix them afterward. Prefer working code over perfect formatting during active troubleshooting.
- Do not reformat unrelated code in the same commit as a bug fix — keep formatting changes in separate commits to preserve clean git blame.

Testing:
- When running tests after a change, first run only the tests relevant to the code you modified. Use `--filter` to target specific test classes or methods (e.g., `--filter "FullyQualifiedName~ParserTests"`) rather than running the entire test suite.
- Only run the full test suite if the targeted tests pass and you have reason to believe the change could affect other areas.

Concurrency:
- Multiple agents or terminals may be building or running tests concurrently in this workspace. To avoid file-lock conflicts on the dotnetup executable and build outputs, **always build and test into an isolated output directory** using `/p:ArtifactsDir=`.
- Choose a short, descriptive name based on what you are working on (e.g., the bug, feature, or test class name). Use that name to create a unique artifacts path under `d:\sdk\artifacts\tmp\`.
- Build command:  `d:\sdk\.dotnet\dotnet build d:\sdk\src\Installer\dotnetup\dotnetup.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\<descriptive-name>\"`
- Test command:   `d:\sdk\.dotnet\dotnet test d:\sdk\test\dotnetup.Tests\dotnetup.Tests.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\<descriptive-name>\"`
- Use the **same** `/p:ArtifactsDir=` value for both the build and the test so the test project picks up the build output.
- Example for a parser fix:
  ```
  d:\sdk\.dotnet\dotnet build d:\sdk\src\Installer\dotnetup\dotnetup.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\parser-fix\"
  d:\sdk\.dotnet\dotnet test d:\sdk\test\dotnetup.Tests\dotnetup.Tests.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\parser-fix\" --filter "FullyQualifiedName~ParserTests"
  ```
- Clean up temporary artifacts directories when you are done: `Remove-Item -Recurse -Force d:\sdk\artifacts\tmp\<descriptive-name>`
