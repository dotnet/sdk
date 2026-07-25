# Finding the versioned SDK root from dotnet-aot

Companion to [DESIGN.md](DESIGN.md).

In a deployed SDK the muxer `dotnet.exe` (in the install root) loads
`dotnet-aot.dll` from the versioned SDK directory and calls `dotnet_execute`
directly. Code in that native bubble needs the versioned SDK directory - the
folder with `dotnet.dll`, `MSBuild.dll`, `Sdks/`, `DotnetTools/`, and the SDK
targets - to resolve SDK-relative paths, but the BCL "where am I" APIs do not
point there.

## Two directories

| Concept | Deployed example | Contains |
|---|---|---|
| Install root (`DOTNET_ROOT`) | `C:\Program Files\dotnet` | muxer `dotnet.exe`, `host\fxr\`, shared runtimes, `sdk\` |
| Versioned SDK root | `C:\Program Files\dotnet\sdk\11.0.100` | `dotnet.dll`, `dotnet-aot.dll`, `MSBuild.dll`, `Sdks\`, `DotnetTools\`, targets |

Code that needs the **root** (workloads, shared runtime, hostfxr) is fine because
the muxer is the process. Code that needs the **versioned SDK directory** (MSBuild,
SDK-shipped tools, targets, forwarders) is what needs care.

## Why the BCL location APIs return the root

The managed CLI is hosted through hostfxr, which sets `APP_CONTEXT_BASE_DIRECTORY`
to the managed entry assembly's directory (the versioned SDK directory), so
`AppContext.BaseDirectory` is correct there. `dotnet-aot.dll` is a NativeAOT shared
library that the muxer loads directly: there is no hostfxr step, nothing sets
`APP_CONTEXT_BASE_DIRECTORY`, and the location APIs fall back to the process
executable - the muxer, i.e. the install root.

Inside the AOT bubble:

| API | Result |
|---|---|
| `AppContext.BaseDirectory` | install root (muxer dir) |
| `Environment.ProcessPath`, `GetCommandLineArgs()[0]`, `Process.MainModule.FileName` | muxer exe |
| `RuntimeEnvironment.GetRuntimeDirectory()` | muxer dir |
| `Assembly.Location` / `typeof(T).Assembly.Location` | `""` (and ILC fails the build with `IL3000`) |
| self-locating the loaded module (`GetModuleFileName` / `dladdr`) | the real `dotnet-aot` directory |

Only two mechanisms yield the versioned SDK directory: the host passes it in, or
the module self-locates.

## Resolution

- **Host argument (authoritative).** The muxer already resolves the versioned SDK
  directory to find `dotnet-aot`, and passes it as the `sdk_dir` argument to
  `dotnet_execute`.
  > `sdk_dir` is the absolute path of the versioned SDK directory that contains the
  > invoked `dotnet-aot` binary.
- **Self-locate (fallback).** When `sdk_dir` is missing or empty, `SdkRootLocator`
  finds the `dotnet-aot` module from its own code address: Windows
  `GetModuleHandleEx(FROM_ADDRESS | UNCHANGED_REFCOUNT, &export)` +
  `GetModuleFileName`; Unix `dladdr`.
- **Publish once.** `NativeEntryPoint.ExecuteCore` publishes the resolved directory as
  the `Microsoft.DotNet.Sdk.Root` AppContext value so the compiled-in assemblies find
  it without threading a parameter through every call. An AppContext value is
  process-local - unlike an environment variable it is not inherited by child
  processes. A caller-provided value (e.g. a `runtimeconfig.json` `configProperties`
  entry) is honored, but must point to an existing directory or the bridge errors out.
- **Read.** In-repo code reads `SdkPaths.SdkDirectory` (in
  `Microsoft.DotNet.Cli.Utils`), which resolves the `Microsoft.DotNet.Sdk.Root`
  AppContext value -> the SDK assembly directory -> `AppContext.BaseDirectory` (once,
  cached). Out-of-repo code (MSBuild tasks, NuGet, the runtime) reads the
  `Microsoft.DotNet.Sdk.Root` AppContext value first, else its existing BCL logic.

The managed CLI keeps using `AppContext.BaseDirectory`, where it is correct.

## Testing

`dn.exe` and `run-dn.ps1` provide a separated layout that places `dotnet-aot.dll`
in a `sdk\<version>\` subdirectory while `dn.exe` stays in the parent, mirroring the
deployed muxer. A flat layout hides SDK-directory bugs because
`AppContext.BaseDirectory` equals `sdk_dir` there by accident. Tests assert that
`--info`'s `Base Path` is the SDK subdirectory in both the passed-`sdk_dir` and
self-locate cases.
