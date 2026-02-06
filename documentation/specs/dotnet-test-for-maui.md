# Specification:  `dotnet test` for Mobile and Device Platforms

## Overview

This document describes the design and implementation plan for enabling `dotnet test` to work seamlessly with mobile and device test projects targeting iOS, Android, and other platforms.  The goal is to allow developers and AI agents to write and run tests on simulators, emulators, and physical devices using the familiar `dotnet test` command-line experience, powered by the Microsoft Testing Platform (MTP).

This functionality leverages the `dotnet run` extensibility infrastructure (specifically `--device` and `--list-devices` arguments) as defined in the [dotnet run for MAUI spec](https://github.com/dotnet/sdk/blob/main/documentation/specs/dotnet-run-for-maui.md).

> **Scope:** This functionality is **not MAUI-specific**. It will be implemented in the **iOS SDK** and **Android SDK** as first-class platform features, available to all .NET workloads including native iOS/Android apps, MAUI, Uno Platform, Avalonia, and other frameworks. 

## Status

| Stage | Status |
|-------|--------|
| Design | 🟡 In Progress |
| Implementation | 🔴 Not Started |
| Testing | 🔴 Not Started |
| Documentation | 🔴 Not Started |

## Authors

- .NET MAUI Team
- Microsoft Testing Platform Team

## Related Documents

- [dotnet run for MAUI](https://github.com/dotnet/sdk/blob/main/documentation/specs/dotnet-run-for-maui.md)
- [dotnet test for MAUI (detailed design)](https://github.com/dotnet/maui/pull/33117)
- [Microsoft Testing Platform Overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro)
- [MTP Server Mode](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Platform/ServerMode)
- [`dotnet test` CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)

---

## 1. Goals and Motivation

### 1.1 Primary Goals

1. **Unified CLI Experience for iOS and Android Testing**:  Enable `dotnet test` to work with **any test project** targeting iOS and Android platforms - including native iOS/Android apps, MAUI apps, Uno Platform, Avalonia, and other frameworks.

2. **Reuse `dotnet run` Infrastructure**:  Leverage the `--device` and `--list-devices` arguments from `dotnet run` extensibility to provide consistent device selection across both commands.

3. **AI Agent Enablement**: Make it easy for AI coding agents to write, run, and validate tests using only command-line tools.  AI agents should be able to: 
   - Run tests with a single command
   - Parse test results from standard output
   - Understand which tests passed/failed

4. **XHarness Replacement**: Eventually replace XHarness as the test execution tool, reducing the number of tools to maintain in the ecosystem.

### 1.2 Non-Goals (Phase 1)

- Test Explorer integration in Visual Studio or VS Code (CLI-only focus for v1)
- Hot reload during test execution
- Parallel test execution across multiple devices
- Code coverage on devices (future phase)
- Live debugging of tests in v1

---

## 2. Integration with `dotnet run` Infrastructure

### 2.1 Shared Device Selection

`dotnet test` will reuse the device selection infrastructure from `dotnet run`:

| Feature | `dotnet run` | `dotnet test` |
|---------|--------------|---------------|
| `--device <id>` | ✅ Select device to run app | ✅ Select device to run tests |
| `--list-devices` | ✅ List available devices | ✅ List available devices |
| `ComputeAvailableDevices` target | ✅ Provides device list | ✅ Reuses same target |
| `DeployToDevice` target | ✅ Deploys app to device | ✅ Reuses same target |

### 2.2 Shared MSBuild Targets

The following MSBuild targets from `dotnet run` will be reused:

```xml
<!-- From iOS/Android workloads - shared between dotnet run and dotnet test -->

<!-- Returns @(Devices) items with available devices -->
<Target Name="ComputeAvailableDevices" Returns="@(Devices)" />

<!-- Deploys the application to the selected device -->
<Target Name="DeployToDevice" />

<!-- Computes run arguments including device selection -->
<Target Name="ComputeRunArguments" />
```

### 2.3 Device Item Format

The `@(Devices)` item format is shared between `dotnet run` and `dotnet test`:

```xml
<ItemGroup>
  <!-- Android examples -->
  <Devices Include="emulator-5554"  Description="Pixel 7 - API 35" Type="Emulator" Status="Offline" />
  <Devices Include="emulator-5555"  Description="Pixel 7 - API 36" Type="Emulator" Status="Online" />
  <Devices Include="0A041FDD400327" Description="Pixel 7 Pro"      Type="Device"   Status="Online" />
  <!-- iOS examples -->
  <Devices Include="94E71AE5-8040-4DB2-8A9C-6CD24EF4E7DE" Description="iPhone 11 - iOS 18.6" Type="Simulator" Status="Shutdown" />
  <Devices Include="FBF5DCE8-EE2B-4215-8118-3A2190DE1AD7" Description="iPhone 14 - iOS 26.0" Type="Simulator" Status="Booted" />
  <Devices Include="AF40CC64-2CDB-5F16-9651-86BCDF380881" Description="My iPhone 15"         Type="Device"    Status="Paired" />
</ItemGroup>
```

---

## 3. Test Pipeline Architecture

### 3.1 High-Level Test Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Developer/AI Agent                              │
│                                                                              │
│    dotnet test MyMauiTests.csproj -f net10.0-ios --device "iPhone 15"       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           dotnet test (SDK)                                  │
│                                                                              │
│  1. Parse arguments (including --device, --list-devices from dotnet run)    │
│  2. Select target framework (interactive prompt if multi-targeted)          │
│  3. Select device (interactive prompt or --device)                          │
│  4. Build test application                                                   │
│  5. Deploy to device (reuse DeployToDevice target)                          │
│  6. Execute tests                                                            │
│  7. Collect artifacts (TRX, logs)                                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              ▼                       ▼                       ▼
    ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
    │ iOS Simulator   │     │ Android Emulator│     │ Windows/Mac     │
    │ / Device        │     │ / Device        │     │ (Local Exe)     │
    │                 │     │                 │     │                 │
    │ ┌─────────────┐ │     │ ┌─────────────┐ │     │ ┌─────────────┐ │
    │ │ Test App    │ │     │ │ Test App    │ │     │ │ Test App    │ │
    │ │ + MTP Host  │ │     │ │ + MTP Host  │ │     │ │ + MTP Host  │ │
    │ └─────────────┘ │     │ └─────────────┘ │     │ └─────────────┘ │
    └────────┬────────┘     └────────┬────────┘     └────────┬────────┘
             │                       │                       │
             └───────────────────────┼───────────────────────┘
                                     │
                                     ▼
                        ┌─────────────────────────┐
                        │  Artifact Collection    │
                        │  - TRX test results     │
                        │  - Console/app logs     │
                        │  - Crash diagnostics    │
                        └─────────────────────────┘
```

### 3.2 Test-Specific MSBuild Targets

New targets specific to `dotnet test` that extend the `dotnet run` infrastructure:

```xml
<!-- Compute test-specific run arguments -->
<Target Name="ComputeTestRunArguments" 
        DependsOnTargets="ComputeRunArguments"
        Returns="@(TestRunArguments)">
  
  <ItemGroup>
    <TestRunArguments Include="--results-directory $(TestResultsDirectory)" />
    <TestRunArguments Include="--report-trx" Condition="'$(GenerateTrx)' == 'true'" />
  </ItemGroup>
</Target>

<!-- Execute tests on device -->
<Target Name="ExecuteDeviceTests"
        DependsOnTargets="DeployToDevice;ComputeTestRunArguments">
  
  <ExecuteDeviceTests
    Device="$(Device)"
    RuntimeIdentifier="$(RuntimeIdentifier)"
    TestArguments="@(TestRunArguments)"
    ResultsDirectory="$(TestResultsDirectory)"
    Timeout="$(TestTimeout)" />
</Target>

<!-- Pull test artifacts from device -->
<Target Name="CollectTestArtifacts"
        AfterTargets="ExecuteDeviceTests">
  
  <CollectDeviceArtifacts
    Device="$(Device)"
    SourcePath="$(DeviceTestResultsPath)"
    DestinationPath="$(TestResultsDirectory)" />
</Target>
```

---

## 4. Command-Line Interface

### 4.1 Basic Usage

```bash
# Run all tests on default device (interactive mode)
dotnet test -f net10.0-ios

# Run tests with explicit project
dotnet test --project MyMauiTests.csproj -f net10.0-ios

# Run tests on specific device (reuses --device from dotnet run)
dotnet test --project MyMauiTests.csproj -f net10.0-android --device "Pixel 7 - API 35"

# List available devices (reuses --list-devices from dotnet run)
dotnet test --project MyMauiTests.csproj -f net10.0-android --list-devices

# Filter tests
dotnet test --project MyMauiTests.csproj -f net10.0-ios --filter "FullyQualifiedName~Button"

# Output formats
dotnet test --project MyMauiTests.csproj -f net10.0-ios --logger trx --logger html
```

### 4.2 Non-Interactive Mode (CI/AI)

```bash
# Non-interactive with explicit device
dotnet test --project MyMauiTests.csproj \
  -f net10.0-ios \
  --device "iossimulator-arm64:iPhone 15" \
  --no-restore \
  --logger "console;verbosity=detailed"

# With TRX output for AI agent analysis
dotnet test --project MyMauiTests.csproj \
  -f net10.0-android \
  --device "emulator-5554" \
  --results-directory ./TestResults \
  -- --report-trx --report-trx-filename results.trx
```

### 4.3 Command-Line Switches

| Switch | Source | Description |
|--------|--------|-------------|
| `--device <id>` | From `dotnet run` | Device/simulator identifier |
| `--list-devices` | From `dotnet run` | List available devices |
| `--filter <expr>` | Existing `dotnet test` | Filter tests to run |
| `--logger <logger>` | Existing `dotnet test` | Specify test logger |
| `--results-directory <path>` | Existing `dotnet test` | Output directory for results |

---

## 5. MTP Configuration

### 5.1 Enabling MTP Mode

Enable MTP mode via `global.json`:

```json
{
  "test":  {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

### 5.2 Device Test Project Structure

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-windows10.0.19041.0</TargetFrameworks>
    <UseMaui>true</UseMaui>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- MTP-based runners -->
    <PackageReference Include="MSTest" Version="*" />
    <PackageReference Include="MSTest.Sdk" Version="*" />
    
    <!-- TRX reporting extension -->
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="*" />
  </ItemGroup>

</Project>
```

---

## 6. Artifact Collection Strategy

### 6.1 V1:  File-Based Artifacts (Recommended for MVP)

**Mechanism:** Test app writes TRX/logs to its sandbox; tooling pulls files back after completion. 

**Artifacts Collected:**
- TRX test results
- Console/app logs
- Crash diagnostics (`.ips` files on iOS, tombstones on Android)
- Screenshots (if applicable)

**Platform-Specific Collection:**

| Platform | Pull Method |
|----------|-------------|
| iOS Simulator | `xcrun simctl get_app_container` + file copy |
| iOS Device | `xcrun devicectl` or file sync |
| Android Emulator/Device | `adb pull` |
| Windows | Direct file access |
| macOS Catalyst | Direct file access |

### 6.2 V2: Streamed Events (Future Enhancement)

For real-time progress reporting, a streaming protocol can be added: 
- Host starts a local results receiver (HTTP/WebSocket)
- App connects and pushes events/results in real-time
- Enables AI agents to react mid-run

---

## 7. Implementation Phases

### Phase 1: Foundation (2-3 weeks)

**Goal:** Basic `dotnet test` working on Android emulator and iOS simulator

**Work Items:**
1.  Integrate `--device` and `--list-devices` from `dotnet run` into `dotnet test`
2. Create file-based artifact collection for iOS/Android
3. Implement MSBuild targets for test execution
4. Basic console output of test results

**Deliverables:**
- `dotnet test --device` works for iOS simulator and Android emulator
- `dotnet test --list-devices` shows available devices
- TRX file retrieved from device

### Phase 2: Cross-Platform & Physical Devices (2-3 weeks)

**Goal:** Full platform support including physical devices

**Work Items:**
1. Physical device support (iOS devices, Android devices)
2. Device artifact collection (logs, screenshots, crash diagnostics)
3. Windows support
4. Improved error messages and diagnostics

### Phase 3: Polish & Documentation (2-3 weeks)

**Goal:** Production-ready experience

**Work Items:**
1. Performance optimization
2. Comprehensive documentation and samples
3. Device-test project templates

---

## 8. Success Criteria

### 8.1 Functional

- [ ] `dotnet test --list-devices` lists available devices (reusing `dotnet run` infrastructure)
- [ ] `dotnet test --device <id>` runs tests on specified device
- [ ] `dotnet test` prompts for device selection in interactive mode
- [ ] Test results appear in console output
- [ ] TRX output format works correctly
- [ ] Works on iOS simulator, Android emulator, Windows

### 8.2 Developer Experience

- [ ] AI agents can run tests with single command
- [ ] Error messages are clear and actionable
- [ ] Consistent experience between `dotnet run` and `dotnet test` for device selection
- [ ] Migration from XHarness is straightforward


---

## 9. References

1. [dotnet run for MAUI Spec (SDK PR #51337)](https://github.com/dotnet/sdk/pull/51337)
2. [dotnet test for MAUI Design (MAUI PR #33117)](https://github.com/dotnet/maui/pull/33117)
3. [Microsoft Testing Platform Source](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Platform)
4. [XHarness Repository](https://github.com/dotnet/xharness)