# .NET SDK Telemetry Documentation

## Table of Contents

- [.NET SDK Telemetry Documentation](#net-sdk-telemetry-documentation)
  - [Table of Contents](#table-of-contents)
  - [How to Control Telemetry](#how-to-control-telemetry)
  - [Common Properties Collected](#common-properties-collected)
  - [Telemetry Events](#telemetry-events)
    - [Core CLI Events](#core-cli-events)
    - [Template Engine Events](#template-engine-events)
    - [SDK-Collected Build Events](#sdk-collected-build-events)
    - [MSBuild Engine Telemetry](#msbuild-engine-telemetry)
    - [Container Events](#container-events)
    - [`dotnet new` Command MSBuild Evaluation](#dotnet-new-command-msbuild-evaluation)
  - [How to update this document](#how-to-update-this-document)

## How to Control Telemetry

### Disabling Telemetry

The .NET SDK telemetry can be disabled using the following environment variable:

- **`DOTNET_CLI_TELEMETRY_OPTOUT`**: Set to `1`, `true`, or `yes` to opt-out of telemetry collection
  - Example: `export DOTNET_CLI_TELEMETRY_OPTOUT=1` (Linux/macOS)
  - Example: `set DOTNET_CLI_TELEMETRY_OPTOUT=1` (Windows)
  - Note: this value is defaulted to `true` on non-Microsoft-provided builds of the .NET SDK (e.g. those provided by our Linux Distro partners, through Source Build, etc.)

### Related Environment Variables

- **`DOTNET_NOLOGO`**: Set to `true` to hide .NET welcome and telemetry messages on first run
  - Values: `true`, `1`, or `yes` to mute messages
  - Values: `false`, `0`, or `no` to allow messages
  - Default: `false` (messages are displayed)
  - Note: This flag does not affect telemetry collection itself

### Telemetry Configuration

- **Default Behavior**:
  - For Microsoft official builds: Telemetry is **enabled by default** (opt-out model)
  - For non-Microsoft builds: Telemetry is **disabled by default** (controlled by `MICROSOFT_ENABLE_TELEMETRY` compile flag)

- **Connection String**: Telemetry data is sent to Application Insights with instrumentation key: `74cc1c9e-3e6e-4d05-b3fc-dde9101d0254`

- **First Time Use**: Telemetry is only collected after the first-time-use notice has been shown and accepted (tracked via sentinel file)

- **Event Namespace**: All telemetry events are automatically prefixed with `dotnet/cli/`

## Common Properties Collected

Every telemetry event automatically includes these common properties:

| Property | Description | Example Value |
|----------|-------------|---------------|
| **OS Version** | Operating system version | `Microsoft Windows 10.0.14393` |
| **OS Platform** | Operating system platform | `Windows`, `Linux`, `OSX` |
| **OS Architecture** | Operating system architecture | `X64`, `X86`, `Arm64` |
| **Output Redirected** | Whether console output is redirected | `True` or `False` |
| **Runtime Id** | .NET runtime identifier | `win10-x64`, `linux-x64` |
| **Product Version** | .NET SDK version | `8.0.100` |
| **Telemetry Profile** | Custom telemetry profile (if set via env var) | Custom value or null |
| **Docker Container** | Whether running in Docker container | `True` or `False` |
| **CI** | Whether running in CI environment | `True` or `False` |
| **LLM** | Detected LLM/assistant environment identifiers (comma-separated) | `claude` or `cursor` |
| **Current Path Hash** | SHA256 hash of current directory path | Hashed value |
| **Machine ID** | SHA256 hash of machine MAC address (or GUID if unavailable) | Hashed value |
| **Machine ID Old** | Legacy machine ID for compatibility | Hashed value |
| **Device ID** | Device identifier | Device-specific ID |
| **Kernel Version** | OS kernel version | `Darwin 17.4.0`, `Linux 4.9.0` |
| **Installation Type** | How the SDK was installed | Installation method |
| **Product Type** | Type of .NET product | Product identifier |
| **Libc Release** | Libc release information | Libc release version |
| **Libc Version** | Libc version information | Libc version number |
| **SessionId** | Unique session identifier | GUID |

## Telemetry Events

### Core CLI Events

#### `command/finish`

**When fired**: At the end of every dotnet CLI command execution

**Properties**:

- `exitCode`: The exit code of the command

**Description**: Tracks the completion status of any dotnet command

---

#### `schema`

**When fired**: When CLI schema is generated or accessed

**Properties**:

- `command`: The command hierarchy as a string

**Description**: Tracks schema generation for the CLI

---

#### `toplevelparser/command`

**When fired**: For every top-level dotnet command

**Properties**:

- `verb`: Top-level command name (build, restore, publish, etc.)

**Measurements**: Performance data (startup time, parse time, etc.)

**Description**: Tracks usage patterns of main dotnet commands

---

#### `sublevelparser/command`

**When fired**: For subcommands and specific command options

**Properties**: Various depending on command (verbosity, configuration, framework, etc.)

**Description**: Tracks detailed command option usage

---

#### `commandresolution/commandresolved`

**When fired**: When a command is resolved through the command factory

**Properties**:

- `commandName`: SHA256 hash of command name
- `commandResolver`: Type of command resolver used

**Description**: Tracks how commands are resolved in the CLI

---

#### `install/reportsuccess`

**When fired**: When installation success is reported

**Properties**:

- `exeName`: Name of the install method that was used - one of `debianpackage`, a specific macOS `.pkg` file name, or a specific Windows exe installer file name.

**Description**: Tracks successful tool installations

---

#### `mainCatchException/exception`

**When fired**: When unhandled exceptions occur in the main CLI

**Properties**:

- `exceptionType`: Type of exception
- `detail`: Exception details (sensitive message removed)

**Description**: Tracks unhandled exceptions for diagnostics

### Template Engine Events

#### `template/new-install`

**When fired**: During template package installation

**Properties**:

- `CountOfThingsToInstall`: Number of template packages being installed

**Description**: Tracks template package installation operations

---

#### `template/new-create-template`

**When fired**: When creating a new project from template

**Properties**:

- `language`: Template language (C#, F#, VB, etc)
- `argument-error`: Whether there were argument errors ("True"/"False")
- `framework`: Framework choice (only sent for Microsoft-authored templates)
- `template-name`: SHA256 hash of template identity
- `template-short-name`: SHA256 hash of template short names
- `package-name`: SHA256 hash of package name
- `package-version`: SHA256 hash of package version
- `is-template-3rd-party`: Whether template is third-party
- `create-success`: Whether creation succeeded
- `auth`: Authentication choice (only sent for Microsoft-authored templates)

**Description**: Tracks template usage and creation success

### SDK-Collected Build Events

#### `msbuild/targetframeworkeval`

**When fired**: When target framework is evaluated

**Properties**:

- `TargetFrameworkVersion`: Target framework version
- `RuntimeIdentifier`: Runtime identifier
- `SelfContained`: Whether self-contained
- `UseApphost`: Whether using app host
- `OutputType`: Output type (Exe, Library, etc.)
- `UseArtifactsOutput`: Whether using artifacts output
- `ArtifactsPathLocationType`: Artifacts path location type - one of `Explicitly Specified`, `DirectoryBuildPropsFolder`, or `ProjectFolder`.

**Description**: Tracks target framework configurations

---

#### `taskBaseCatchException`

**When fired**: When exceptions occur in SDK-provided MSBuild tasks

**Properties**:

- `exceptionType`: Exception type
- `detail`: Exception details (message removed)

**Description**: Tracks exceptions in MSBuild task execution

---

#### `PublishProperties`

**When fired**: During the Publish Target

**Properties**: Gathers the values of the following MSBuild Properties if they are set in the project file or via command line:

- PublishReadyToRun
- PublishSingleFile
- PublishTrimmed
- PublishAot
- PublishProtocol

**Description**: Tracks publish-related properties

---

#### `WorkloadPublishProperties`

**When fired**: During workload publish operations

**Properties**: Gathers the values of the following MSBuild Properties if they are set in the project file or via command line:

- TargetPlatformIdentifier: TargetPlatformIdentifier
- RuntimeIdentifier: RuntimeIdentifier
- BlazorWasm: _WorkloadUsesBlazorWasm
- WasmSDK: _WorkloadUsesWasmSDK
- UsesMaui: UseMaui
- UsesMobileSDKOnly: _WorkloadUsesMobileSDKOnly
- UsesOtherMobileSDK: _WorkloadUsesOther
- MonoAOT: _WorkloadUsesMonoAOT
- NativeAOT: _WorkloadUsesNativeAOT
- Interp: _WorkloadUsesInterpreter
- LibraryMode: _WorkloadUsesLibraryMode
- ResolvedRuntimePack: _MonoWorkloadRuntimePackPackageVersion
- StripILAfterAOT: _WorkloadUsesStripILAfterAOT

**Description**: Tracks workload-specific publish properties

---

#### `ReadyToRun`

**When fired**: During ReadyToRun compilation

**Properties**: Gathers the values of the following MSBuild Properties and Items if they are set in the project file or via command line:

- PublishReadyToRunUseCrossgen2: PublishReadyToRunUseCrossgen2
- Crossgen2PackVersion: ResolvedCrossgen2Pack.NuGetPackageVersion
- CompileListCount: _ReadyToRunCompileList->Count()
- FailedCount: _ReadyToRunCompilationFailures->Count()

**Description**: Tracks ReadyToRun compilation usage

---

### MSBuild Engine Telemetry

See [MSBuild Telemetry Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/CollectedTelemetry.md) for details on these events.

### Container Events

See [Container Telemetry Documentation](https://github.com/dotnet/sdk-container-builds/blob/main/docs/Telemetry.md) for details.

### `dotnet new` Command MSBuild Evaluation

#### `new/msbuild-eval`

**When fired**: When MSBuild project evaluation occurs during `dotnet new`

**Properties** (hashed):

- `ProjectPath`: Project path
- `SdkStyleProject`: Whether it's SDK-style project
- `Status`: Evaluation status
- `TargetFrameworks`: Target frameworks

**Measurements**:

- `EvaluationTime`: Total evaluation time in milliseconds
- `InnerEvaluationTime`: Inner evaluation time in milliseconds

**Description**: Tracks MSBuild evaluation performance for new projects

## How to update this document

- Ensure that all telemetry events and properties are accurately documented
- Review and update common properties as needed
- Add new events or properties as they are introduced in the .NET SDK
- Follow the pre-existing format
