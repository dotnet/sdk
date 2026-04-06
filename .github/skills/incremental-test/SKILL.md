---
name: incremental-test
description: >-
  Run dotnet.Tests incrementally without a full build.cmd rebuild. Use after
  modifying source code in SDK projects to quickly build only changed projects,
  deploy their outputs into the redist SDK layout, and run tests against them.
---

# Incremental Test Runner for dotnet.Tests

## Prerequisites

A full build must have been completed at least once (`build.cmd` or `build.sh`) so the redist SDK layout exists at `artifacts/bin/redist/Debug/dotnet/sdk/<version>/`.

## Workflow

### Step 1: Build modified projects

Build each modified project using the repo-local dotnet:

```
./.dotnet/dotnet build <path-to-project.csproj> -c Debug
```

### Step 2: Copy outputs to the redist SDK layout

Run the copy script with the project names (matching the directory names under `artifacts/bin/`):

```pwsh
scripts/Copy-ToRedist.ps1 <ProjectName> [<ProjectName2> ...]
```

For example, after modifying `Microsoft.DotNet.Cli.Utils` and `dotnet`:

```pwsh
scripts/Copy-ToRedist.ps1 Microsoft.DotNet.Cli.Utils dotnet
```

The script discovers the SDK version directory, copies only DLLs that are already present in the redist layout, and handles satellite resource assemblies.

### Step 3: Build the test project (if test code was modified)

```
./.dotnet/dotnet build test/dotnet.Tests/dotnet.Tests.csproj
```

This project outputs directly to `artifacts/bin/redist/Debug/` via `TestHostFolder`.

### Step 4: Run the tests

```
./.dotnet/dotnet exec artifacts/bin/redist/Debug/dotnet.Tests.dll -method "*TestMethodName*"
```

Or via `dotnet test`:

```
./.dotnet/dotnet test test/dotnet.Tests/dotnet.Tests.csproj --no-build --filter "Name~TestMethodName"
```

## Gotchas

- Only DLLs **already present** in the redist layout are copied. If your change introduces a new shipped assembly, run a full `build.cmd`/`build.sh` instead.
- Multi-targeting projects (e.g., `net10.0` and `net472`): always use the `net10.0` output.
- The `dotnet` project builds into `artifacts/bin/dotnet/Debug/net10.0/`, not `artifacts/bin/Cli/dotnet/...`.
