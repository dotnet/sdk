---
name: incremental-test
description: >-
  Run dotnet.Tests incrementally without a full build.cmd rebuild. Use after
  modifying source code in SDK projects to quickly build only changed projects,
  deploy their outputs into the redist SDK layout, and run tests against them.
---

# Incremental Test Runner for dotnet.Tests

## Prerequisites

- A full build must have been completed at least once (via `build.cmd` or `build.sh`) so that the redist SDK layout exists at `artifacts\bin\redist\Debug\dotnet\sdk\<version>\`.
- The repo-local `.dotnet` SDK must match the version expected by the test projects. If the runtime or SDK version is out of date (e.g., test build fails with a missing framework error), run `.\restore.cmd` (or `./restore.sh` on macOS/Linux) to download the correct SDK into `.dotnet`.
- This workflow uses Windows/PowerShell commands and paths. On macOS/Linux, substitute forward slashes and use `cp` instead of `Copy-Item`.

## Workflow

### Step 1: Identify modified projects

Determine which projects have been modified. Use context from:
- The files you just edited in this session.
- Or `git status`/`git diff` to find changed `.cs` files and map them to their `.csproj` projects.

### Step 2: Build modified projects

Build each modified project individually using the repo-local dotnet:

```
.\.dotnet\dotnet build <path-to-project.csproj> -c Debug
```

For example:
```
.\.dotnet\dotnet build src\Cli\Microsoft.DotNet.Cli.Utils\Microsoft.DotNet.Cli.Utils.csproj -c Debug
```

If the `dotnet` CLI project itself was modified, build it:
```
.\.dotnet\dotnet build src\Cli\dotnet\dotnet.csproj -c Debug
```

### Step 3: Copy output DLLs to the redist SDK layout

Discover the SDK version directory name:
```powershell
$sdkVersion = (Get-ChildItem artifacts\bin\redist\Debug\dotnet\sdk -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
```

For each modified project, copy its output DLL (and any satellite assemblies) from the project's build output to the redist SDK directory:

```
Source: artifacts\bin\<ProjectName>\Debug\net10.0\<AssemblyName>.dll
Target: artifacts\bin\redist\Debug\dotnet\sdk\<version>\
```

For example:
```powershell
Copy-Item artifacts\bin\Microsoft.DotNet.ProjectTools\Debug\net10.0\Microsoft.DotNet.ProjectTools.dll artifacts\bin\redist\Debug\dotnet\sdk\$sdkVersion\
Copy-Item artifacts\bin\Microsoft.DotNet.Cli.Utils\Debug\net10.0\Microsoft.DotNet.Cli.Utils.dll artifacts\bin\redist\Debug\dotnet\sdk\$sdkVersion\
```

The `dotnet` project is special — it builds into `artifacts\bin\dotnet\Debug\net10.0\` and its `dotnet.dll` must be copied to the SDK directory:
```powershell
Copy-Item artifacts\bin\dotnet\Debug\net10.0\dotnet.dll artifacts\bin\redist\Debug\dotnet\sdk\$sdkVersion\
```

**Important notes:**
- For typical incremental edits, only copy DLLs that are **already present** in the target directory. If your change introduces a new shipped assembly or moves assemblies, you will need a full `build.cmd`/`build.sh` to update the layout correctly.
- Some projects multi-target (e.g., `net10.0` and `net472`). Always use the `net10.0` output.
- If localization resource DLLs were changed (in subdirectories like `cs\`, `de\`, etc.), copy those too.

### Step 4: Build the test project (if test code was modified)

The test project `test\dotnet.Tests\dotnet.Tests.csproj` outputs directly to `artifacts\bin\redist\Debug\` (via `TestHostFolder`), so just build it:

```
.\.dotnet\dotnet build test\dotnet.Tests\dotnet.Tests.csproj
```

### Step 5: Run the tests

Run specific tests:
```
.\.dotnet\dotnet exec artifacts\bin\redist\Debug\dotnet.Tests.dll -method "*TestMethodName*"
```

Or run filtered tests via `dotnet test`:
```
.\.dotnet\dotnet test test\dotnet.Tests\dotnet.Tests.csproj --no-build --filter "Name~TestMethodName"
```

## Common project paths

| Assembly | Project Path |
|---|---|
| `dotnet.dll` | `src\Cli\dotnet\dotnet.csproj` |
| `Microsoft.DotNet.Cli.Utils.dll` | `src\Cli\Microsoft.DotNet.Cli.Utils\Microsoft.DotNet.Cli.Utils.csproj` |
| `Microsoft.DotNet.Cli.Definitions.dll` | `src\Cli\Microsoft.DotNet.Cli.Definitions\Microsoft.DotNet.Cli.Definitions.csproj` |
| `Microsoft.DotNet.Cli.CoreUtils.dll` | `src\Cli\Microsoft.DotNet.Cli.CoreUtils\Microsoft.DotNet.Cli.CoreUtils.csproj` |
| `Microsoft.DotNet.Configurer.dll` | `src\Cli\Microsoft.DotNet.Configurer\Microsoft.DotNet.Configurer.csproj` |
| `Microsoft.DotNet.ProjectTools.dll` | `src\Microsoft.DotNet.ProjectTools\Microsoft.DotNet.ProjectTools.csproj` |
| `Microsoft.DotNet.NativeWrapper.dll` | `src\Resolvers\Microsoft.DotNet.NativeWrapper\Microsoft.DotNet.NativeWrapper.csproj` |
| `Microsoft.DotNet.TemplateLocator.dll` | `src\Microsoft.DotNet.TemplateLocator\Microsoft.DotNet.TemplateLocator.csproj` |
| `Microsoft.DotNet.InternalAbstractions.dll` | `src\Cli\Microsoft.DotNet.InternalAbstractions\Microsoft.DotNet.InternalAbstractions.csproj` |
| `dotnet.Tests.dll` | `test\dotnet.Tests\dotnet.Tests.csproj` |
