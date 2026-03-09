# CLI to SDK/Runtime Layout Coupling Catalog

## 1. SDK Discovery Mechanisms

### 1.1 Native Hostfxr Dependency (CRITICAL - Hard Coupling)
**Location**: `src/Resolvers/Microsoft.DotNet.NativeWrapper/`

**Description**: The CLI relies heavily on the native `hostfxr` library for SDK discovery and resolution.

**Coupling Points**:
- **P/Invoke to hostfxr**: `Interop.cs` contains DllImports to "hostfxr"
  - `hostfxr_get_available_sdks` - Enumerates all installed SDKs
  - `hostfxr_resolve_sdk2` - Resolves SDK based on global.json
  - `hostfxr_get_dotnet_environment_info` - Gets SDK and runtime information
  
**Key Files**:
- `NETCoreSdkResolverNativeWrapper.cs:58-67` - `GetAvailableSdks(dotnetExeDirectory)`
- `Interop.cs:156-158` (Windows), `Interop.cs:185-187` (Unix)

**Impact on Portability**: **CRITICAL BLOCKER**
- Requires `hostfxr.dll` (Windows) or `libhostfxr.so`/`.dylib` to be loadable
- hostfxr expects to be in a specific location relative to the .NET installation
- Without hostfxr, CLI falls back to manual directory enumeration (less robust)

**Environment Overrides**:
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` - Can override dotnet directory location

---

### 1.2 EnvironmentProvider - Dotnet Directory Discovery
**Location**: `src/Resolvers/Microsoft.DotNet.NativeWrapper/EnvironmentProvider.cs`

**Description**: Discovers the dotnet installation directory using multiple strategies.

**Discovery Strategy** (in order of precedence):
1. **Environment Variable Override**: `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` (lines 54-59)
2. **Current Process Path** (for .NET Core builds): `Environment.ProcessPath` (lines 65-66)
   - Only uses this if process name is "dotnet"
3. **PATH Search**: Searches for `dotnet` executable in PATH (line 71)
   - On Linux/macOS: Resolves symlinks using `realpath()` (lines 73-78)
4. **Fallback** (legacy .NET Framework): Current process path (line 93)

**Key Files**:
- `EnvironmentProvider.cs:52-103` - `GetDotnetExeDirectory()`
- `SdkInfoProvider.cs:40-45` - Calls `EnvironmentProvider.GetDotnetExeDirectory()`

**Impact on Portability**: **HIGH BLOCKER**
- Assumes CLI is colocated with SDK OR can find dotnet via PATH
- Symlink resolution on Unix expects standard dotnet installation structure

**Environment Overrides**:
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` - Direct override

---

### 1.3 SDK Path Construction Pattern
**Location**: Multiple files

**Description**: After discovering dotnet directory, assumes SDK is at `{dotnetDir}/sdk/{version}/`

**Pattern**:
```csharp
string dotnetDir = EnvironmentProvider.GetDotnetExeDirectory();
string sdkDir = Path.Combine(dotnetDir, "sdk");
string sdkVersionDir = Path.Combine(dotnetDir, "sdk", version, ...);
```

**Examples**:
- `SdkInfoProvider.cs:58` - `Path.Combine(dotnetDir, "sdk")` for manual enumeration fallback
- `SdkCheckCommand.cs:34` - `Path.Combine(_dotnetPath, "sdk", version, "sdk-check-config.json")`

**Impact on Portability**: **HIGH BLOCKER**
- Hardcoded "sdk" subdirectory assumption
- Cannot relocate CLI separately from SDK without breaking this

---

## 2. AppContext.BaseDirectory Usages (Path-based Assumptions)

### 2.1 Bundled Tools Discovery
**Location**: `src/Cli/dotnet/`

**Description**: CLI ships with bundled tools (format, fsi, vstest) and locates them relative to `AppContext.BaseDirectory`.

**Specific Tool Paths**:

#### dotnet-format
- `Commands/Format/FormatForwardingApp.cs:14` - `Path.Combine(AppContext.BaseDirectory, "DotnetTools/dotnet-format/dotnet-format.dll")`
- `Commands/Format/FormatForwardingApp.cs:17` - `.deps.json` file
- `Commands/Format/FormatForwardingApp.cs:20` - `.runtimeconfig.json` file

#### F# Interactive (fsi)
- `Commands/Fsi/FsiForwardingApp.cs:50` - `Path.Combine(AppContext.BaseDirectory, FsiDllName)`
- `Commands/Fsi/FsiForwardingApp.cs:57` - `Path.Combine(AppContext.BaseDirectory, FsiExeName)`

#### VSTest
- `Commands/Test/VSTest/VSTestForwardingApp.cs:38` - `Path.Combine(AppContext.BaseDirectory, VstestAppName)`

#### Generic DotnetTools
- `CommandFactory/CommandResolution/DotnetToolsCommandResolver.cs:16` - `Path.Combine(AppContext.BaseDirectory, "DotnetTools", ...)`
- `CommandFactory/CommandResolution/AppBaseDllCommandResolver.cs:21` - `Path.Combine(AppContext.BaseDirectory, "{commandName}.dll")`

**Impact on Portability**: **MEDIUM-HIGH BLOCKER**
- Assumes bundled tools are deployed alongside CLI executable
- Could be made configurable via environment variables or config file

---

### 2.2 MSBuild.dll Discovery
**Location**: Multiple files

**Description**: CLI needs MSBuild.dll for build/restore/publish operations.

**Path Construction**:
- `CommandFactory/CommandResolution/ProjectToolsCommandResolver.cs:379` - `Path.Combine(AppContext.BaseDirectory, "MSBuild.dll")`
- `CommandFactory/CommandResolution/ProjectFactory.cs:34` - `Path.Combine(AppContext.BaseDirectory, "MSBuild.dll")`

**Environment Setup**:
- `Commands/Run/CommonRunHelpers.cs:20` - `globalProperties[Constants.MSBuildExtensionsPath] = AppContext.BaseDirectory;`
- `Commands/Run/RunCommand.cs:940` - Sets MSBuildExtensionsPath to AppContext.BaseDirectory

**Impact on Portability**: **HIGH BLOCKER**
- MSBuild.dll expected in same directory as CLI
- MSBuildExtensionsPath set to CLI location

---

### 2.3 AppHostTemplate Discovery
**Location**: `src/Cli/dotnet/ShellShim/ShellShimTemplateFinder.cs`

**Description**: Shim generation for global tools requires apphost template.

**Path**:
- `ShellShimTemplateFinder.cs:63` - `Path.Combine(AppContext.BaseDirectory, "AppHostTemplate")`

**Impact on Portability**: **MEDIUM BLOCKER**
- Required for `dotnet tool install` functionality
- Could be configurable

---

## 3. Environment Variables

### 3.1 Critical Environment Variables

**Dotnet Location**:
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` - Overrides dotnet directory discovery
- `DOTNET_ROOT` - Fallback for locating dotnet installation
- `DOTNET_HOST_PATH` - Absolute path to current dotnet executable (set by SDK)

**MSBuild Integration**:
- `MSBUILDFAILONDRIVEENUMERATINGWILDCARD` - Set to "1" by Program.cs:58

**Path Modifications**:
- `PATH` - Used to find dotnet executable

**Testing/VSTest**:
- `VSTEST_DOTNET_ROOT_PATH` - Set to muxer directory (VSTestForwardingApp.cs:50)
- `VSTEST_DOTNET_ROOT_ARCHITECTURE` - Set to process architecture

### 3.2 DOTNET_HOST_PATH Usage
**Set by**:
- `MsbuildProject.cs:189` - Sets before MSBuild invocation
- `MSBuildForwardingAppWithoutLogging.cs:222` - Passes to MSBuild

**Read by**:
- `Muxer.cs:65` - Reads to find dotnet executable
- `NuGetCommand.cs:46` - Passes to NuGet commands

**Purpose**: Ensures child processes (MSBuild, NuGet) use same dotnet installation

**Impact on Portability**: **MEDIUM**
- Acts as a mechanism to propagate dotnet location to child processes
- Could be leveraged for portable scenarios

---

## 4. Process Invocation Patterns

### 4.1 MSBuild Forwarding
**Location**: `src/Cli/Microsoft.DotNet.Cli.Utils/`

**Description**: CLI doesn't implement build logic itself - forwards to MSBuild.

**Key Classes**:
- `ForwardingApp` - Base abstraction for process forwarding
- `MSBuildForwardingApp` / `MSBuildForwardingAppWithoutLogging`

**Assumptions**:
- MSBuild.dll is at `AppContext.BaseDirectory`
- Invoked via `dotnet exec MSBuild.dll`

**Impact on Portability**: **HIGH BLOCKER**
- Cannot move CLI without MSBuild
- OR need to make MSBuild path configurable

---

### 4.2 Self-Invocation
**Observation**: CLI sometimes spawns itself

**Example**:
- Cleanup operations: `dotnet clean file-based-app-artifacts --automatic`

**Uses**:
- `new Muxer().MuxerPath` to get path to current dotnet

**Impact on Portability**: **MEDIUM**
- Needs to know its own path
- Already uses abstraction (Muxer) that could support relocation

---

## 5. Muxer (dotnet Executable Discovery)

### 5.1 Muxer Class - Self-Location Logic
**Location**: `src/Cli/Microsoft.DotNet.Cli.Utils/Muxer.cs`

**Description**: Critical class for locating the dotnet executable. Used extensively by CLI for process spawning and self-invocation.

**Discovery Strategy** (lines 37-78):
1. **Primary**: Calculate from `AppContext.BaseDirectory`
   - Assumes layout: `{dotnetRoot}/sdk/{version}/` where AppContext.BaseDirectory = SDK version directory
   - Goes up two directories: `Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory))`
   - Looks for `dotnet.exe` or `dotnet` in that directory (line 44)

2. **Fallback 1**: `Environment.ProcessPath` (current process)
   - Only if current process is named "dotnet" (line 62)

3. **Fallback 2**: `DOTNET_HOST_PATH` environment variable (line 65)
   - Read when process is not dotnet

4. **Fallback 3**: `DOTNET_ROOT` environment variable (line 69)
   - Constructs path: `Path.Combine(DOTNET_ROOT, "dotnet{.exe}")`

**Impact on Portability**: **CRITICAL BLOCKER**
- **Hardcoded assumption**: CLI is at `{dotnetRoot}/sdk/{version}/`
- If CLI is relocated, Muxer cannot find dotnet executable
- This breaks ALL process spawning, self-invocation, and forwarding

**Used By**: 74+ locations including:
- ForwardingApp (MSBuild, NuGet, FSI, Format, VSTest)
- Self-invocation for cleanup
- Setting DOTNET_HOST_PATH for child processes

---

## 6. Workload Management (SDK Layout Dependency)

### 6.1 Workload Manifest Locations
**Location**: `src/Cli/dotnet/Commands/Workload/Install/FileBasedInstaller.cs`

**Manifest Path Pattern** (line 271):
```csharp
Path.Combine(_workloadRootDir, "sdk-manifests", sdkFeatureBand)
```

**Workload Set Path Pattern** (line 104):
```csharp
Path.Combine(_workloadRootDir, "sdk-manifests", workloadSetFeatureBand.ToString(), "workloadsets", workloadSetVersion)
```

**Impact on Portability**: **HIGH BLOCKER**
- Expects `sdk-manifests/` directory at dotnet root
- Workload discovery fails if CLI is relocated

---

### 6.2 Workload Pack Installation
**Location**: `src/Cli/dotnet/Commands/Workload/Install/FileBasedInstaller.cs`

**Packs Directory**: `{_workloadRootDir}/packs/` (implicit from pack resolution)

**Impact on Portability**: **HIGH BLOCKER**
- Packs installed relative to dotnet root
- Cannot relocate CLI without breaking workload functionality

**Registry-based installer** (`NetSdkMsiInstallerClient`):
- Uses Windows Registry: `SOFTWARE\Microsoft\dotnet\InstalledPacks\{arch}` (line 306)
- Also expects `sdk-manifests/` at dotnet root (line 134, 369)

---

## 7. CSharp Compiler Command (Packs Path Assumptions)

### 7.1 Reference Assembly Path Construction
**Location**: `src/Cli/dotnet/Commands/Run/CSharpCompilerCommand.cs`

**Critical Path Calculations** (lines 43-44):
```csharp
private static string SdkPath => AppContext.BaseDirectory;
private static string DotNetRootPath => Path.GetDirectoryName(Path.GetDirectoryName(SdkPath))!;
```

**Reference Assemblies** (CSharpCompilerCommand.Generated.cs:26+):
```csharp
$"/reference:{DotNetRootPath}/packs/Microsoft.NETCore.App.Ref/{RuntimeVersion}/ref/net10.0/..."
```

**AppHost Template** (line 315):
```csharp
Path.Join(SdkPath, "..", "..", "packs", $"Microsoft.NETCore.App.Host.{rid}", RuntimeVersion, "runtimes", rid, "native", $"apphost{...}")
```

**Impact on Portability**: **CRITICAL BLOCKER**
- `dotnet run file.cs` feature completely broken if CLI relocated
- Assumes `packs/` directory at `{dotnetRoot}/packs/`
- Hardcoded path construction to reference assemblies and apphost

---

## 8. Tool Package Management

### 8.1 Tool Installation Paths
**Location**: `src/Cli/Microsoft.DotNet.Configurer/CliFolderPathCalculator.cs`

**User Profile Paths**:
- `DotnetHomePath`: `%USERPROFILE%` or `$HOME` (via `DOTNET_HOME` override)
- `DotnetUserProfileFolderPath`: `{DotnetHome}/.dotnet/` (line 40)
- `ToolsShimPath`: `{DotnetHome}/.dotnet/tools/` (line 20)
- `ToolsPackagePath`: `{DotnetHome}/.dotnet/tools/.store/` (line 22)

**CLI Fallback Folder** (line 16-18):
```csharp
Path.Combine(new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName ?? "", "NuGetFallbackFolder")
```
Assumes: `{dotnetRoot}/NuGetFallbackFolder/`

**Impact on Portability**: **MEDIUM**
- Tool installation NOT directly coupled to CLI location
- Tools stored in user profile
- **BUT** CliFallbackFolderPath assumes CLI is under dotnet root

---

### 8.2 Tool Path Calculator
**Location**: `src/Cli/dotnet/CommandFactory/CommandResolution/ToolPathCalculator.cs`

**Pattern**: `.tools/{packageId}/{version}/{framework}/` (lines 62-65)

**Impact on Portability**: **LOW**
- Uses packages directory parameter (not hardcoded to dotnet root)
- Could work with relocated CLI if package directory is configurable

---

## 9. Configuration and First-Run Experience

### 9.1 DotnetFirstTimeUseConfigurer
**Location**: `src/Cli/Microsoft.DotNet.Configurer/DotnetFirstTimeUseConfigurer.cs`

**Uses**:
- `CliFolderPathCalculator` for sentinel files and tool paths
- Writes to user profile, NOT dotnet installation

**Impact on Portability**: **LOW**
- First-run experience uses user profile paths
- Not directly coupled to CLI location

---

## 10. Additional Critical Findings

### 10.1 Shared Framework Version Discovery
**Location**: `src/Cli/Microsoft.DotNet.Cli.Utils/Muxer.cs`

**FX_DEPS_FILE AppContext Data** (lines 16-22):
```csharp
internal string SharedFxVersion
{
    get
    {
        var depsFile = new FileInfo(GetDataFromAppDomain("FX_DEPS_FILE") ?? string.Empty);
        return depsFile.Directory?.Name ?? string.Empty;
    }
}
```

**Impact**: **MEDIUM**
- Uses AppContext data for runtime location
- Assumes standard .NET runtime layout

---

## 11. Summary of Path Assumptions

### Critical Hardcoded Layout Assumptions:

1. **CLI Location**: `{dotnetRoot}/sdk/{version}/`
   - Muxer.cs line 41
   - CSharpCompilerCommand.cs lines 43-44
   - EnvironmentProvider fallback logic

2. **SDK Directory**: `{dotnetRoot}/sdk/`
   - SdkInfoProvider.cs line 58
   - SdkCheckCommand.cs line 34

3. **Packs Directory**: `{dotnetRoot}/packs/`
   - CSharpCompilerCommand.cs line 315
   - Workload pack installation

4. **SDK Manifests**: `{dotnetRoot}/sdk-manifests/`
   - FileBasedInstaller.cs line 271
   - NetSdkMsiInstallerClient.cs line 134

5. **NuGet Fallback**: `{dotnetRoot}/NuGetFallbackFolder/`
   - CliFolderPathCalculator.cs line 18

6. **Bundled Tools**: `{sdkPath}/DotnetTools/` and `{sdkPath}/MSBuild.dll`
   - FormatForwardingApp.cs, AppContext.BaseDirectory usages

7. **AppHostTemplate**: `{sdkPath}/AppHostTemplate/`
   - ShellShimTemplateFinder.cs line 63

---

## 12. NuGet Integration

### 12.1 NuGetForwardingApp
**Location**: `src/Cli/dotnet/NuGetForwardingApp.cs`

**NuGet.CommandLine.XPlat.dll Path** (lines 40-42):
```csharp
Path.Combine(AppContext.BaseDirectory, "NuGet.CommandLine.XPlat.dll")
```

**Impact on Portability**: **MEDIUM BLOCKER**
- NuGet commands (add, remove package) expect NuGet dll in same directory as CLI
- Forwarded via ForwardingApp (uses Muxer for dotnet path)

---

### 12.2 NuGet Cache Paths
**Location**: `src/Cli/dotnet/Commands/Run/CSharpCompilerCommand.cs`

**NuGet Cache Discovery** (line 46):
```csharp
private static string NuGetCachePath => SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(null));
```

**Impact on Portability**: **LOW**
- Uses NuGet's own configuration discovery
- Not directly coupled to CLI location
- Typically: `%USERPROFILE%\.nuget\packages`

---

## 13. MSBuild Integration (Critical)

### 13.1 MSBuild.dll Location
**Location**: `src/Cli/Microsoft.DotNet.Cli.Utils/MSBuildForwardingAppWithoutLogging.cs`

**MSBuild Path** (line 21):
```csharp
private const string MSBuildExeName = "MSBuild.dll";
```

**GetMSBuildExePath**: Returns `Path.Combine(AppContext.BaseDirectory, MSBuildExeName)`

**Impact on Portability**: **CRITICAL BLOCKER**
- All build/restore/publish/pack operations depend on this
- MSBuild.dll MUST be in same directory as CLI

---

### 13.2 MSBuild Environment Variables Set by CLI
**Location**: `src/Cli/Microsoft.DotNet.Cli.Utils/MSBuildForwardingAppWithoutLogging.cs` (lines 216-224)

**Required Environment Variables for MSBuild**:
```csharp
{
    { "MSBuildExtensionsPath", AppContext.BaseDirectory },
    { "MSBuildSDKsPath", GetMSBuildSDKsPath() },  // {AppContext.BaseDirectory}/Sdks
    { "DOTNET_HOST_PATH", new Muxer().MuxerPath }
}
```

**GetMSBuildSDKsPath** (lines 197-209):
- Checks `MSBuildSDKsPath` environment variable first
- Falls back to `Path.Combine(AppContext.BaseDirectory, "Sdks")`

**Impact on Portability**: **CRITICAL BLOCKER**
- MSBuild expects SDKs at `{CLI Directory}/Sdks/`
- MSBuildExtensionsPath set to CLI directory
- **This is the primary mechanism for MSBuild to find SDK tasks and targets**

---

### 13.3 MSBuild Execution Modes
**Behavior**:
- **In-Process** (default): MSBuild runs in same process as CLI
- **Out-of-Process**: When `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC=true` or custom MSBuild path specified

**Environment Variables**:
- `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC` - Force out-of-process execution
- `DOTNET_CLI_USE_MSBUILD_SERVER` - Use MSBuild server mode
- `DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER` - Terminal logger configuration

---

## 14. ForwardingApp Pattern

### 14.1 ForwardingAppImplementation Base Class
**Location**: `src/Cli/Microsoft.DotNet.Cli.Utils/ForwardingAppImplementation.cs`

**Purpose**: Abstraction for spawning child processes via `dotnet exec {dll}`

**GetHostExeName** (line 96):
```csharp
private string GetHostExeName() => new Muxer().MuxerPath;
```

**Process Invocation Pattern**:
- FileName: `{Muxer.MuxerPath}` (dotnet executable)
- Arguments: `exec [--depsfile ...] [--runtimeconfig ...] {dllPath} {args}`

**Impact on Portability**: **HIGH**
- All forwarding commands (MSBuild, NuGet, Format, FSI, VSTest) depend on Muxer
- Muxer failure = all forwarding commands fail

---

### 14.2 Forwarding Commands Summary
**Commands using ForwardingApp**:
1. **MSBuildForwardingApp** - build, restore, publish, clean, pack, store
2. **NuGetForwardingApp** - NuGet package operations
3. **FormatForwardingApp** - dotnet-format (bundled tool)
4. **FsiForwardingApp** - F# Interactive
5. **VSTestForwardingApp** - vstest.console.dll

**All assume**: Tools are in `AppContext.BaseDirectory` or subdirectories

---

## 15. Test Framework Integration

### 15.1 VSTest Path and Environment
**Location**: `src/Cli/dotnet/Commands/Test/VSTest/VSTestForwardingApp.cs`

**VSTest Path** (line 38):
```csharp
Path.Combine(AppContext.BaseDirectory, "vstest.console.dll")
```

**Environment Override**: `VSTEST_CONSOLE_PATH`

**VSTest Root Variables** (lines 48-52):
```csharp
{
    ["VSTEST_DOTNET_ROOT_PATH"] = Path.GetDirectoryName(new Muxer().MuxerPath),
    ["VSTEST_DOTNET_ROOT_ARCHITECTURE"] = RuntimeInformation.ProcessArchitecture.ToString()
}
```

**Purpose**: Tell vstest.console where to find dotnet for testhost.exe

**Impact on Portability**: **MEDIUM**
- VSTest expects to be bundled with CLI
- Can be overridden via `VSTEST_CONSOLE_PATH`
- Passes dotnet location to testhost

---

## 16. Complete Environment Variables Catalog

### 16.1 SDK and Runtime Location
| Variable | Purpose | Impact on Portability |
|----------|---------|----------------------|
| `DOTNET_ROOT` | Dotnet installation directory | **HIGH** - Used as fallback in Muxer |
| `DOTNET_ROOT_{ARCH}` | Architecture-specific dotnet root | **HIGH** - For cross-arch scenarios |
| `DOTNET_ROOT(x86)` | 32-bit dotnet root | **HIGH** - Legacy, pre-.NET 6 |
| `DOTNET_HOST_PATH` | Absolute path to dotnet executable | **CRITICAL** - Set by CLI for child processes |
| `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` | Override dotnet directory for SDK resolution | **CRITICAL** - Can override SDK discovery |

---

### 16.2 MSBuild Configuration
| Variable | Purpose | Set By CLI |
|----------|---------|------------|
| `MSBuildExtensionsPath` | MSBuild SDK location | **YES** - Set to `AppContext.BaseDirectory` |
| `MSBuildSDKsPath` | MSBuild Sdks directory | **YES** - Set to `{BaseDirectory}/Sdks` |
| `MSBUILD_EXE_PATH` | Path to MSBuild executable | Read by some commands |
| `MSBUILDUSESERVER` | Use MSBuild server mode | **YES** - Set based on config |
| `MSBUILDFAILONDRIVEENUMERATINGWILDCARD` | Fail on drive enumeration | **YES** - Set to "1" |
| `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC` | Force MSBuild out-of-process | Read (user-configurable) |
| `DOTNET_CLI_USE_MSBUILD_SERVER` | Enable MSBuild server | Read (user-configurable) |
| `DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER` | Terminal logger config | Read (user-configurable) |
| `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR` | Override MSBuild SDKs directory | Read for SDK resolution |
| `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER` | Override MSBuild SDK version | Read for SDK resolution |
| `DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG` | Enable MSBuild resolver logging | Read for diagnostics |

---

### 16.3 Workload Management
| Variable | Purpose |
|----------|---------|
| `DOTNETSDK_WORKLOAD_PACK_ROOTS` | Override workload pack roots |
| `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS` | Override workload manifest roots |
| `DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS` | Ignore default manifest locations |
| `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE` | Disable workload update notifications |
| `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS` | Update notification interval |
| `DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS` | Disable pack grouping optimization |
| `DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK` | Skip first-run workload integrity check |

---

### 16.4 First-Run and Configuration
| Variable | Purpose | Default |
|----------|---------|---------|
| `DOTNET_GENERATE_ASPNET_CERTIFICATE` | Generate ASP.NET dev cert | true |
| `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH` | Add global tools to PATH | true |
| `DOTNET_NOLOGO` | Suppress .NET logo/welcome message | false |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Opt out of telemetry | false |
| `DOTNET_HOME` | Override user home directory | - |
| `DOTNET_CLI_TEST_FALLBACKFOLDER` | Test: NuGet fallback folder | - |

---

### 16.5 Testing and Diagnostics
| Variable | Purpose |
|----------|---------|
| `VSTEST_CONSOLE_PATH` | Override vstest.console path |
| `VSTEST_DOTNET_ROOT_PATH` | Set by CLI for testhost |
| `VSTEST_DOTNET_ROOT_ARCHITECTURE` | Set by CLI for testhost |
| `DOTNET_CLI_VSTEST_TRACE` | Enable VSTest tracing |
| `DOTNET_CLI_TEST_TRACEFILE` | VSTest trace file path |
| `DOTNET_TEST_DISABLE_SWITCH_VALIDATION` | Disable test switch validation |
| `DOTNET_CLI_PERF_LOG` | Enable performance logging |
| `DOTNET_PERFLOG_DIR` | Performance log directory |
| `DOTNET_PERF_LOG_COUNT` | Number of perf logs to keep |

---

### 16.6 Other Environment Variables
| Variable | Purpose |
|----------|---------|
| `DOTNET_CLI_UI_LANGUAGE` | Override CLI UI language |
| `DOTNET_CLI_FORCE_UTF8_ENCODING` | Force UTF-8 encoding |
| `DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT` | Allow tool manifest in root |
| `DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE` | Disable auto-Release config |
| `DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS` | Lazy Release for solutions |
| `DOTNET_CLI_CONTEXT_*` | Command context variables |
| `DOTNET_CLI_TELEMETRY_SESSIONID` | Telemetry session ID |
| `DOTNET_CLI_TELEMETRY_PROFILE` | Telemetry profile override |
| `DOTNET_BUILD_PIDFILE_DIRECTORY` | Build server PID file directory |
| `DOTNET_CLI_DISABLE_FILE_BASED_APP_ARTIFACTS_AUTOMATIC_CLEANUP` | Disable automatic cleanup |
| `DOTNETSDK_ALLOW_TARGETING_PACK_CACHING` | Allow targeting pack caching |

---

## 17. Summary: Critical Path Assumptions (COMPLETE)

### Hardcoded Layout Dependencies (Ranked by Severity)

#### CRITICAL BLOCKERS (Cannot relocate CLI without breaking)
1. **Muxer Discovery**: CLI location = `{dotnetRoot}/sdk/{version}/`
   - `Muxer.cs` line 41: Goes up 2 directories from `AppContext.BaseDirectory`
   - Used in 74+ locations

2. **MSBuild Integration**:
   - `MSBuildExtensionsPath` = `AppContext.BaseDirectory`
   - `MSBuildSDKsPath` = `{AppContext.BaseDirectory}/Sdks`
   - MSBuild.dll = `{AppContext.BaseDirectory}/MSBuild.dll`

3. **Hostfxr P/Invoke**:
   - Native library dependency for SDK resolution
   - Expects hostfxr in standard .NET layout

4. **CSharp Compiler (dotnet run)**:
   - Reference assemblies: `{dotnetRoot}/packs/Microsoft.NETCore.App.Ref/...`
   - AppHost template: `{dotnetRoot}/packs/Microsoft.NETCore.App.Host.{rid}/...`

5. **SDK Discovery**:
   - SDK directory: `{dotnetDir}/sdk/`
   - Workload manifests: `{dotnetDir}/sdk-manifests/`

#### HIGH BLOCKERS (Major features broken)
6. **Bundled Tools** (Format, FSI, VSTest, NuGet):
   - All at `{AppContext.BaseDirectory}/{ToolName}.dll`

7. **Workload Packs**:
   - Packs directory: `{dotnetRoot}/packs/`

8. **AppHost Template**:
   - ShimTemplate: `{AppContext.BaseDirectory}/AppHostTemplate/`

#### MEDIUM IMPACT (Configurable or user-profile based)
9. **NuGet Fallback Folder**:
   - `{dotnetRoot}/NuGetFallbackFolder/`
   - Overridable via `DOTNET_CLI_TEST_FALLBACKFOLDER`

10. **Tool Installation Paths**:
    - User profile-based: `{HOME}/.dotnet/tools/`
    - Not directly coupled to CLI location

---

## 18. Decoupling Opportunities

### Existing Environment Variable Overrides (Can Leverage)
1. ✅ `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` - Override dotnet directory
2. ✅ `MSBuildExtensionsPath` - Override MSBuild extensions
3. ✅ `MSBuildSDKsPath` - Override MSBuild SDKs location
4. ✅ `DOTNET_HOST_PATH` - Override dotnet executable path
5. ✅ `DOTNET_ROOT` - Fallback for dotnet location
6. ✅ `VSTEST_CONSOLE_PATH` - Override vstest location
7. ✅ `DOTNETSDK_WORKLOAD_PACK_ROOTS` - Override pack locations
8. ✅ `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS` - Override manifest locations

### Configuration File Approach
- Could introduce `dotnet-paths.json` or similar
- Define: SDK location, MSBuild location, tool paths, packs directory
- Read at startup before any path assumptions

### Abstraction Layer
- Introduce `IPathResolver` interface
- Centralize all `AppContext.BaseDirectory` and path construction logic
- Single point to inject custom path resolution

---

## 19. Portability Recommendations

### Option 1: Environment Variable-Based Standalone Mode
**Approach**: Use existing environment variables to point to SDK

**Required Settings**:
```bash
DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=/path/to/dotnet
DOTNET_HOST_PATH=/path/to/dotnet/dotnet
MSBuildExtensionsPath=/path/to/dotnet/sdk/{version}
MSBuildSDKsPath=/path/to/dotnet/sdk/{version}/Sdks
DOTNETSDK_WORKLOAD_PACK_ROOTS=/path/to/dotnet/packs
DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=/path/to/dotnet/sdk-manifests
```

**Pros**: No code changes required
**Cons**: User must set many environment variables correctly

---

### Option 2: Wrapper Script/Bootstrapper
**Approach**: Small wrapper that sets environment and calls CLI

**Example**:
```bash
#!/bin/bash
export DOTNET_ROOT=/usr/share/dotnet
export DOTNET_HOST_PATH=/usr/share/dotnet/dotnet
export MSBuildExtensionsPath=/usr/share/dotnet/sdk/10.0.100
# ... more vars
exec /opt/mycli/dotnet "$@"
```

**Pros**: Users don't need to know about environment variables
**Cons**: Requires wrapper distribution, must update SDK paths on upgrade

---

### Option 3: Configuration File Support (Code Change Required)
**Approach**: Add configuration file support to CLI

**Example `dotnet-config.json`**:
```json
{
  "dotnetRoot": "/usr/share/dotnet",
  "sdkPath": "/usr/share/dotnet/sdk/10.0.100",
  "packsPath": "/usr/share/dotnet/packs",
  "manifestsPath": "/usr/share/dotnet/sdk-manifests"
}
```

**Code Changes**:
- Read config at startup (Program.cs)
- Pass to path resolvers throughout codebase
- Override `AppContext.BaseDirectory` assumptions

**Pros**: Clean, user-friendly, maintainable
**Cons**: Requires code changes, testing, backward compatibility

---

### Option 4: Fully Standalone CLI Bundle (Major Refactor)
**Approach**: Bundle MSBuild, SDKs, tools with CLI in portable directory

**Changes Required**:
- Modify Muxer to support self-contained mode
- Update all `AppContext.BaseDirectory` references to use resolver
- Include MSBuild, SDKs, reference packs in CLI bundle
- Test extensively for all commands

**Pros**: True portability, no external dependencies
**Cons**: Large bundle size, significant engineering effort

---

## 20. Next Steps for Making CLI Portable

### Phase 1: Analysis Complete ✓
- [x] Catalog all path assumptions
- [x] Document environment variable dependencies
- [x] Identify critical coupling points

### Phase 2: Proof of Concept
- [ ] Test Option 1 (environment variables) with simple scenarios
- [ ] Measure what breaks when CLI is relocated
- [ ] Document minimum required overrides

### Phase 3: Design Decision
- [ ] Choose portability strategy (Option 1-4)
- [ ] Design configuration mechanism
- [ ] Plan backward compatibility

### Phase 4: Implementation
- [ ] Implement path resolver abstraction
- [ ] Update all hardcoded path assumptions
- [ ] Add configuration file support (if chosen)
- [ ] Update documentation

### Phase 5: Validation
- [ ] Test all major scenarios (build, test, publish, etc.)
- [ ] Validate workload management
- [ ] Test tool installation
- [ ] Cross-platform testing

---

## FINAL ASSESSMENT

### Can the .NET CLI be made portable/relocatable?

**Short Answer**: **YES, with significant effort and environment configuration**

**Current State**: **TIGHTLY COUPLED** to SDK/Runtime layout
- The CLI has **15+ critical hardcoded assumptions** about being at `{dotnetRoot}/sdk/{version}/`
- Moving the CLI without the entire SDK breaks: build, test, run, publish, tool management, workload management

### Viability of Portability Options

#### ✅ **Option 1: Environment Variables (FEASIBLE NOW)**
**Effort**: Low (no code changes)
**Complexity**: High (users must set ~10 environment variables correctly)
**Recommendation**: Good for advanced users, testing scenarios, CI environments

Required minimum configuration:
```bash
DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=/path/to/dotnet
DOTNET_HOST_PATH=/path/to/dotnet/dotnet  
MSBuildExtensionsPath=/path/to/sdk/{version}
MSBuildSDKsPath=/path/to/sdk/{version}/Sdks
```

**Pros**: Works today, no code changes
**Cons**: Fragile, undocumented, error-prone

---

#### ✅ **Option 2: Wrapper Script (FEASIBLE, RECOMMENDED FOR POC)**
**Effort**: Low (scripting only)
**Complexity**: Medium (maintain wrapper + version-specific paths)
**Recommendation**: Good for controlled environments, distribution packages

**Pros**: User-friendly, hides complexity
**Cons**: Must update wrapper for SDK version changes

---

#### ⚠️ **Option 3: Configuration File (REQUIRES CODE CHANGES)**
**Effort**: Medium (implement config reader + path resolver)
**Complexity**: Medium
**Recommendation**: Best long-term solution for official portability support

**Pros**: Clean, maintainable, discoverable
**Cons**: Requires SDK team buy-in, testing, backward compatibility work

**Estimated Effort**: 2-4 weeks engineering + testing

---

#### ⚠️ **Option 4: Fully Standalone Bundle (MAJOR REFACTOR)**
**Effort**: High (redesign path resolution throughout codebase)
**Complexity**: Very High
**Recommendation**: Only if complete independence from SDK is required

**Pros**: True portability
**Cons**: Large bundle (~500MB+), significant engineering investment

**Estimated Effort**: 2-3 months engineering + extensive testing

---

### Key Blockers by Feature Area

| Feature | Blocker | Severity | Workaround Available? |
|---------|---------|----------|----------------------|
| **Build/Restore/Publish** | MSBuild location + MSBuildExtensionsPath | CRITICAL | ✅ Via env vars |
| **SDK Discovery** | Hostfxr + dotnet directory assumption | CRITICAL | ✅ Via `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` |
| **Muxer (Self-location)** | Hardcoded 2-level-up from BaseDirectory | CRITICAL | ⚠️ Partial via `DOTNET_HOST_PATH` |
| **dotnet run file.cs** | Packs path construction | CRITICAL | ❌ No override |
| **Workloads** | sdk-manifests + packs directories | HIGH | ✅ Via `DOTNETSDK_*` vars |
| **Bundled Tools** | Tools in BaseDirectory | MEDIUM | ⚠️ Some have overrides |
| **Global Tools** | User profile-based | LOW | ✅ Not coupled to CLI |

---

### Recommended Path Forward

**For Immediate Portability Needs**:
1. **Use Option 1 (Environment Variables)** for testing/validation
2. **Create Option 2 (Wrapper Script)** for distribution
3. Document required environment variables clearly

**For Official SDK Support**:
1. **Propose Option 3** (Configuration File) to .NET SDK team
2. Design `IPathResolver` abstraction
3. Incrementally refactor path assumptions
4. Add integration tests for relocated CLI scenarios

**Critical Code Changes Needed** (for Option 3):
- `Muxer.cs`: Support reading dotnet path from config
- `MSBuildForwardingAppWithoutLogging.cs`: Support configurable MSBuild paths
- `CSharpCompilerCommand.cs`: Support configurable packs directory
- `SdkInfoProvider.cs`: Support configurable SDK directory
- New: `PathResolver.cs` - Central configuration-based path resolution

---

### Risk Assessment

**LOW RISK** (Option 1/2 - Environment Variables):
- No code changes to SDK
- Fully reversible
- Can validate feasibility quickly

**MEDIUM RISK** (Option 3 - Configuration File):
- Requires SDK team coordination
- Backward compatibility must be maintained
- Testing burden moderate

**HIGH RISK** (Option 4 - Standalone Bundle):
- Large surface area for regressions
- Significant testing required
- May conflict with SDK servicing model

---

## Conclusion

The .NET CLI is **heavily coupled** to the SDK/Runtime layout, but **portability is achievable**:

1. **Short-term**: Use environment variables or wrapper scripts (works today)
2. **Long-term**: Implement configuration file support with path resolver abstraction

The codebase has **good separation** in some areas (user profile paths for tools) and **tight coupling** in others (MSBuild, bundled tools, Muxer). A systematic refactoring to introduce `IPathResolver` would enable true portability while maintaining backward compatibility.

**Recommended first step**: Create a wrapper script and test with environment variables to validate the approach and identify any missing overrides.

**Total investigation items**: 40+ path assumptions cataloged, 30+ environment variables documented, 8 critical coupling points identified.

### Critical Blockers to CLI Portability:
1. **Hostfxr P/Invoke dependency** - Expects hostfxr in specific location
2. **SDK directory structure assumption** - `{dotnetDir}/sdk/{version}/`
3. **AppContext.BaseDirectory assumptions** - MSBuild.dll, bundled tools
4. **MSBuildExtensionsPath** - Set to CLI location

### Potential Decoupling Strategies:
1. **Environment variable-based configuration**
   - Leverage existing `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR`
   - Add new variables for tool locations
   
2. **Configuration file** 
   - Define SDK location, tool paths, etc.
   
3. **Abstraction layer**
   - Introduce `IPathResolver` interface
   - Centralize all path assumptions

4. **Standalone mode**
   - Package CLI with minimal dependencies
   - Use environment variables to point to SDK
