# .NET SDK Telemetry Documentation

## Table of Contents
- [How to Control Telemetry](#how-to-control-telemetry)
- [Common Properties Collected](#common-properties-collected)
- [Telemetry Events](#telemetry-events)
  - [Core CLI Events](#core-cli-events)
  - [Template Engine Events](#template-engine-events)
  - [MSBuild Events](#msbuild-events)
  - [Container Events](#container-events)
  - [New Command MSBuild Evaluation](#new-command-msbuild-evaluation)

## How to Control Telemetry

### Disabling Telemetry

The .NET SDK telemetry can be disabled using the following environment variable:

- **`DOTNET_CLI_TELEMETRY_OPTOUT`**: Set to `1`, `true`, or `yes` to opt-out of telemetry collection
  - Example: `export DOTNET_CLI_TELEMETRY_OPTOUT=1` (Linux/macOS)
  - Example: `set DOTNET_CLI_TELEMETRY_OPTOUT=1` (Windows)
  - Note: this value is defaulted to `true` on non-Microsoft-provided builds of the .NET SDK (e.g. those provided by our Linux Distro partners, through Source Build, etc.)
  - **Source**: [Environment variable definition](../../src/Common/EnvironmentVariableNames.cs#L18) | [Opt-out check](../../src/Cli/dotnet/Telemetry/Telemetry.cs#L47) | [Default value](../../src/Common/CompileOptions.cs#L13-L18)

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
  - **Source**: [Compile-time configuration](../../src/Common/CompileOptions.cs#L5-L25)

- **Connection String**: Telemetry data is sent to Application Insights with instrumentation key: `74cc1c9e-3e6e-4d05-b3fc-dde9101d0254`

- **First Time Use**: Telemetry is only collected after the first-time-use notice has been shown and accepted (tracked via sentinel file)

- **Event Namespace**: All telemetry events are automatically prefixed with `dotnet/cli/`

## Common Properties Collected

Every telemetry event automatically includes these common properties:

**Source**: [TelemetryCommonProperties.cs](../../src/Cli/dotnet/Telemetry/TelemetryCommonProperties.cs#L57-L88)

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
**Source**: [Program.cs:288](../../src/Cli/dotnet/Program.cs#L288)
**Properties**:
- `exitCode`: The exit code of the command

**Description**: Tracks the completion status of any dotnet command

---

#### `schema`
**When fired**: When CLI schema is generated or accessed
**Source**: [CliSchema.cs:78](../../src/Cli/dotnet/CliSchema.cs#L78)
**Properties**:
- `command`: The command hierarchy as a string

**Description**: Tracks schema generation for the CLI

---

#### `toplevelparser/command`
**When fired**: For every top-level dotnet command
**Source**: [TelemetryFilter.cs:59](../../src/Cli/dotnet/Telemetry/TelemetryFilter.cs#L59)
**Properties**:
- `verb`: Top-level command name (build, restore, publish, etc.)
- `globalJson`: Global.json state information (if available)

**Measurements**: Performance data (startup time, parse time, etc.)

**Description**: Tracks usage patterns of main dotnet commands

---

#### `sublevelparser/command`
**When fired**: For subcommands and specific command options
**Source**: [TelemetryFilter.cs:153](../../src/Cli/dotnet/Telemetry/TelemetryFilter.cs#L153)
**Properties**: Various depending on command (verbosity, configuration, framework, etc.)

**Description**: Tracks detailed command option usage

---

#### `commandresolution/commandresolved`
**When fired**: When a command is resolved through the command factory
**Source**: [CompositeCommandResolver.cs:41](../../src/Cli/dotnet/CommandFactory/CommandResolution/CompositeCommandResolver.cs#L41)
**Properties**:
- `commandName`: SHA256 hash of command name
- `commandResolver`: Type of command resolver used

**Description**: Tracks how commands are resolved in the CLI

---

#### `install/reportsuccess`
**When fired**: When installation success is reported
**Source**: [TelemetryFilter.cs:76](../../src/Cli/dotnet/Telemetry/TelemetryFilter.cs#L76)
**Properties**:
- `exeName`: Name of the executable being installed

**Description**: Tracks successful tool installations

---

#### `mainCatchException/exception`
**When fired**: When unhandled exceptions occur in the main CLI
**Source**: [TelemetryFilter.cs:82](../../src/Cli/dotnet/Telemetry/TelemetryFilter.cs#L82)
**Properties**:
- `exceptionType`: Type of exception
- `detail`: Exception details (sensitive message removed)

**Description**: Tracks unhandled exceptions for diagnostics

### Template Engine Events

#### `template/new-install`
**When fired**: During template package installation
**Source**: [TemplatePackageCoordinator.cs:193](../../src/Cli/Microsoft.TemplateEngine.Cli/TemplatePackageCoordinator.cs#L193)
**Properties**:
- `CountOfThingsToInstall`: Number of template packages being installed

**Description**: Tracks template package installation operations

---

#### `template/new-create-template`
**When fired**: When creating a new project from template
**Source**: [TemplateInvoker.cs:81](../../src/Cli/Microsoft.TemplateEngine.Cli/TemplateInvoker.cs#L81)
**Properties**:
- `language`: Template language (hashed if Microsoft-authored)
- `argument-error`: Whether there were argument errors ("True"/"False")
- `framework`: Framework choice (hashed if Microsoft-authored)
- `template-name`: SHA256 hash of template identity
- `template-short-name`: SHA256 hash of template short names
- `package-name`: SHA256 hash of package name
- `package-version`: SHA256 hash of package version
- `is-template-3rd-party`: Whether template is third-party
- `create-success`: Whether creation succeeded
- `auth`: Authentication choice (hashed if Microsoft-authored)

**Description**: Tracks template usage and creation success

### MSBuild Events

#### `msbuild/targetframeworkeval`
**When fired**: When target framework is evaluated
**Source**: [MSBuildLogger.cs:114](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L114)
**Properties** (all hashed):
- `TargetFrameworkVersion`: Target framework version
- `RuntimeIdentifier`: Runtime identifier
- `SelfContained`: Whether self-contained
- `UseApphost`: Whether using app host
- `OutputType`: Output type (Exe, Library, etc.)
- `UseArtifactsOutput`: Whether using artifacts output
- `ArtifactsPathLocationType`: Artifacts path location type

**Description**: Tracks target framework configurations

---

#### `msbuild/build`
**When fired**: During MSBuild build operations
**Source**: [MSBuildLogger.cs:137](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L137)
**Properties** (hashed):
- `ProjectPath`: SHA256 hash of project path
- `BuildTarget`: SHA256 hash of build target

**Measurements**:
- `BuildDurationInMilliseconds`: Build duration
- `InnerBuildDurationInMilliseconds`: Inner build duration

**Description**: Tracks build performance and targets

---

#### `msbuild/loggingConfiguration`
**When fired**: When logging configuration is processed
**Source**: [MSBuildLogger.cs:142](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L142)
**Measurements**:
- `FileLoggersCount`: Number of file loggers configured

**Description**: Tracks MSBuild logging setup

---

#### `msbuild/buildcheck/acquisitionfailure`
**When fired**: When buildcheck acquisition fails
**Source**: [MSBuildLogger.cs:147](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L147)
**Properties** (hashed):
- `AssemblyName`: Failed assembly name
- `ExceptionType`: Exception type
- `ExceptionMessage`: Exception message

**Description**: Tracks buildcheck acquisition failures

---

#### `msbuild/buildcheck/run`
**When fired**: When buildcheck runs
**Source**: [MSBuildLogger.cs:152](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L152)
**Measurements**:
- `TotalRuntimeInMilliseconds`: Total runtime

**Description**: Tracks buildcheck execution performance

---

#### `msbuild/buildcheck/rule`
**When fired**: When buildcheck rules are processed
**Source**: [MSBuildLogger.cs:157](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L157)
**Properties** (hashed):
- `RuleId`: Rule identifier
- `CheckFriendlyName`: Check friendly name

**Measurements**:
- `TotalRuntimeInMilliseconds`: Runtime for the rule

**Description**: Tracks individual buildcheck rule performance

---

#### `taskBaseCatchException`
**When fired**: When exceptions occur in MSBuild tasks
**Source**: [TaskBase.cs:44](../../src/Tasks/Common/TaskBase.cs#L44)
**Properties**:
- `exceptionType`: Exception type
- `detail`: Exception details (message removed)

**Description**: Tracks exceptions in MSBuild task execution

---

#### `PublishProperties`
**When fired**: During publish operations
**Source**: [MSBuildLogger.cs:163](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L163)

**Description**: Tracks publish-related properties

---

#### `WorkloadPublishProperties`
**When fired**: During workload publish operations
**Source**: [MSBuildLogger.cs:165](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L165)

**Description**: Tracks workload-specific publish properties

---

#### `ReadyToRun`
**When fired**: During ReadyToRun compilation
**Source**: [MSBuildLogger.cs:164](../../src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs#L164)

**Description**: Tracks ReadyToRun compilation usage

### Container Events

#### `sdk/container/inference`
**When fired**: When base container image is inferred
**Source**: [ComputeDotnetBaseImageAndTag.cs](../../src/Containers/Microsoft.NET.Build.Containers/Tasks/ComputeDotnetBaseImageAndTag.cs)
**Properties**:
- `ProjectType`: Type of project
- `PublishMode`: Publish mode used
- `IsInvariant`: Whether using invariant globalization
- `TargetRuntimes`: Target runtime identifiers

**Description**: Tracks container base image inference decisions

---

#### `sdk/container/publish/success`
**When fired**: When container publish succeeds
**Source**: [Telemetry.cs:68](../../src/Containers/Microsoft.NET.Build.Containers/Telemetry.cs#L68)
**Properties**:
- `RemotePullType`: Remote pull registry type
- `LocalPullType`: Local pull storage type
- `RemotePushType`: Remote push registry type
- `LocalPushType`: Local push storage type

**Description**: Tracks successful container publishes

---

#### `sdk/container/publish/error`
**When fired**: When container publish encounters errors
**Source**: [Telemetry.cs:75](../../src/Containers/Microsoft.NET.Build.Containers/Telemetry.cs#L75)
**Properties**:
- `RemotePullType`: Remote pull registry type
- `LocalPullType`: Local pull storage type
- `RemotePushType`: Remote push registry type
- `LocalPushType`: Local push storage type
- `error`: Error type (`unknown_repository`, `credential_failure`, `rid_mismatch`, etc.)
- `direction`: Direction for credential failures (`pull`/`push`)
- Additional error-specific properties

**Description**: Tracks container publish failures and error types

### New Command MSBuild Evaluation

#### `new/msbuild-eval`
**When fired**: When MSBuild project evaluation occurs during `dotnet new`
**Source**: [MSBuildEvaluator.cs:212](../../src/Cli/dotnet/Commands/New/MSBuildEvaluation/MSBuildEvaluator.cs#L212)
**Properties** (hashed):
- `ProjectPath`: Project path
- `SdkStyleProject`: Whether it's SDK-style project
- `Status`: Evaluation status
- `TargetFrameworks`: Target frameworks

**Measurements**:
- `EvaluationTime`: Total evaluation time in milliseconds
- `InnerEvaluationTime`: Inner evaluation time in milliseconds

**Description**: Tracks MSBuild evaluation performance for new projects

## Privacy and Data Handling

### Data Protection Measures

1. **Hashing**: Sensitive data like file paths, project names, and machine identifiers are SHA256 hashed before transmission
2. **No Personal Information**: No personally identifiable information is collected
3. **No Source Code**: No source code or file contents are collected
4. **Exception Sanitization**: Exception messages are removed, only exception types are collected
5. **Opt-out Available**: Users can completely disable telemetry at any time

### Data Usage

The telemetry data is used by Microsoft to:
- Understand .NET SDK usage patterns
- Identify and fix common issues
- Improve performance and reliability
- Guide feature development priorities
- Monitor adoption of new features

### Compliance

The .NET SDK telemetry system complies with Microsoft's privacy policies and data handling practices. For more information, see [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).
