# ProcessFrameworkReferences Task Output Specification

## Overview

The `ProcessFrameworkReferences` MSBuild task is responsible for resolving framework references and determining which NuGet packages need to be downloaded to support compilation and publishing scenarios. This task produces several output item groups that represent different types of packages required for the build.

## Output Item Groups

The task produces the following output collections:

- **`TargetingPacks`**: Reference assemblies used during compilation
- **`RuntimePacks`**: Runtime-specific implementations of framework libraries
- **`PackagesToDownload`**: Packages that need to be restored via direct PackageDownload mechanism
- **`RuntimeFrameworks`**: Framework dependencies to be written to runtimeconfig.json
- **`ImplicitPackageReferences`**: Build-time tool packages that need to be restored via PackageReference mechanism (ILLink, Crossgen2, ILCompiler)
- **`Crossgen2Packs`**: Ready-to-Run compilation tools
- **`HostILCompilerPacks`**: Native AOT compiler tools for the host platform
- **`TargetILCompilerPacks`**: Native AOT compiler tools for the target platform
- **`UnavailableRuntimePacks`**: Framework packs not available for the specified RID

## Expected Outputs by Scenario

### Base Scenario: Framework-Dependent Projects

For all projects that reference frameworks (via `<FrameworkReference>` items):

**Expected Outputs:**
- ✅ `TargetingPacks`: One entry per framework reference per target framework
  - Contains the targeting pack name and version
  - Used for compile-time reference resolution
- ✅ `RuntimeFrameworks`: One entry per framework reference per target framework
  - Written to runtimeconfig.json to specify runtime dependencies

**Example:**
```xml
<FrameworkReference Include="Microsoft.NETCore.App" />
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

Results in:
- `TargetingPacks`: `Microsoft.NETCore.App.Ref`, `Microsoft.AspNetCore.App.Ref`
- `RuntimeFrameworks`: `Microsoft.NETCore.App`, `Microsoft.AspNetCore.App`

---

### Scenario 1: Self-Contained Deployment (`SelfContained=true`, `PublishSelfContained=true`)

When `RuntimeIdentifier` is specified explicitly:

**Expected Outputs:**
- ✅ `TargetingPacks`: One per framework reference per TFM
- ✅ `RuntimePacks`: One per framework reference per TFM for the specified RID
  - Contains RID-specific runtime libraries
  - Example: `Microsoft.NETCore.App.Runtime.linux-x64`
- ✅ `PackagesToDownload`: Includes both targeting packs and runtime packs
- ✅ `RuntimeFrameworks`: One per framework reference per TFM

**Key Properties:**
```xml
<SelfContained>true</SelfContained>
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
```

**Behavior:**
- Runtime packs are resolved for the primary `RuntimeIdentifier`
- RID-specific assets are included in the publish output

---

### Scenario 2: Self-Contained with multiple RuntimeIdentifiers (`RuntimeIdentifiers` property)

When multiple RIDs are specified via `RuntimeIdentifiers`:

**Expected Outputs:**
- ✅ `TargetingPacks`: One per framework reference per TFM
- ✅ `RuntimePacks`: 
  - For the primary RID (if `RuntimeIdentifier` is set): Full runtime pack metadata
  - For additional RIDs: Runtime packs are downloaded but not added to `RuntimePacks` output
- ✅ `PackagesToDownload`: Runtime packs for ALL RIDs in `RuntimeIdentifiers`
- ✅ `RuntimeFrameworks`: One per framework reference per TFM

**Key Properties:**
```xml
<SelfContained>true</SelfContained>
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
<RuntimeIdentifiers>linux-x64;linux-arm64;win-x64</RuntimeIdentifiers>
```

**Behavior:**
- All runtime packs for all RIDs are downloaded to support multi-RID publishing
- Only the primary RID's runtime packs appear in the `RuntimePacks` output for consumption

---

### Scenario 3: Ready-to-Run Compilation (`ReadyToRunEnabled=true`, `ReadyToRunUseCrossgen2=true`)

For projects using Ready-to-Run (R2R) compilation:

**Expected Outputs:**
- ✅ `TargetingPacks`: One per framework reference per TFM
- ✅ `RuntimePacks`: One per framework reference per TFM for the specified RID
- ✅ `Crossgen2Packs`: RID-specific Crossgen2 compiler for the host platform
  - Example: `Microsoft.NETCore.App.Crossgen2.linux-x64`
- ✅ `PackagesToDownload`: Includes targeting packs, runtime packs (if applicable), and Crossgen2 pack
- ✅ `RuntimeFrameworks`: One per framework reference per TFM

**Key Properties:**
```xml
<SelfContained>true</SelfContained>
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
<ReadyToRunEnabled>true</ReadyToRunEnabled>
<ReadyToRunUseCrossgen2>true</ReadyToRunUseCrossgen2>
```

**Behavior:**
- Crossgen2 pack is resolved based on the SDK's runtime identifier (host RID)
- Enables ahead-of-time compilation of IL to native code for faster startup

---

### Scenario 4: Publish with Trimming (`PublishTrimmed=true`, `RequiresILLinkPack=true`)

For projects that trim unused code during publish:

**Expected Outputs:**
- ✅ `TargetingPacks`: One per framework reference per TFM
- ✅ `RuntimePacks`: One per framework reference per TFM for the specified RID
  - **Required even when `SelfContained=false`** because trimming requires RID-specific analysis
- ✅ `ImplicitPackageReferences`: `Microsoft.NET.ILLink.Tasks`
  - Contains MSBuild targets and tasks for trimming
- ✅ `PackagesToDownload`: Includes targeting packs, runtime packs, and ILLink pack
- ✅ `RuntimeFrameworks`: One per framework reference per TFM

**Key Properties:**
```xml
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
<PublishTrimmed>true</PublishTrimmed>
<RequiresILLinkPack>true</RequiresILLinkPack>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
```

**Behavior:**
- Runtime packs are required for trimming analysis even in framework-dependent deployments
- ILLink pack provides the trimming engine and MSBuild integration
- For .NET 6+, trim analyzer warnings are enabled by default

---

### Scenario 5: Trim and AOT Analysis Support (`IsTrimmable=true`, `EnableTrimAnalyzer=true`, `IsAotCompatible=true`, `EnableAotAnalyzer=true`)

For libraries that want to support trimming and AOT:

**Expected Outputs:**
- ✅ `TargetingPacks`: One per framework reference per TFM
- ✅ `ImplicitPackageReferences`: `Microsoft.NET.ILLink.Tasks`
  - Provides analyzers for trim and AOT compatibility
- ✅ `PackagesToDownload`: Includes targeting packs and ILLink pack
- ✅ `RuntimeFrameworks`: One per framework reference per TFM

**Key Properties:**
```xml
<IsTrimmable>true</IsTrimmable>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<IsAotCompatible>true</IsAotCompatible>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
<RequiresILLinkPack>true</RequiresILLinkPack>
```

**Behavior:**
- No RID specified, so no runtime packs are downloaded
- ILLink pack provides compile-time analyzers for library authors
- Warnings/errors guide developers to make code trim/AOT-compatible

---

### Scenario 6: Native AOT Publishing (`PublishAot=true`)

For projects using Native AOT compilation:

**Expected Outputs:**
- ✅ `TargetingPacks`: One per framework reference per TFM
- ✅ `RuntimePacks`: One per framework reference per TFM for the specified RID
  - **Required even when `SelfContained=false`** because AOT requires RID-specific compilation
- ✅ `HostILCompilerPacks`: ILCompiler pack for the host/SDK RID
  - Example: `runtime.linux-x64.Microsoft.DotNet.ILCompiler`
  - Used to run the AOT compiler
- ✅ `TargetILCompilerPacks`: ILCompiler pack for the target RID (if different from host)
  - Example: `runtime.linux-arm64.Microsoft.DotNet.ILCompiler`
  - Contains target-specific compilation assets
- ✅ `ImplicitPackageReferences`: `Microsoft.NET.ILLink.Tasks`
  - ILLink is used as part of the AOT compilation pipeline
- ✅ `PackagesToDownload`: Includes targeting packs, runtime packs, and ILCompiler packs
- ✅ `RuntimeFrameworks`: One per framework reference per TFM

**Key Properties:**
```xml
<RuntimeIdentifier>linux-arm64</RuntimeIdentifier>
<PublishAot>true</PublishAot>
<RequiresILLinkPack>true</RequiresILLinkPack>
```

**Behavior:**
- Runtime packs are required for AOT compilation regardless of `SelfContained` setting
- Host ILCompiler pack must match the SDK's RID to run the compiler
- Target ILCompiler pack must match the `RuntimeIdentifier` for cross-compilation scenarios
- Native AOT produces a single native executable with no runtime dependencies

---

### Scenario 7: Combined Scenarios

Projects can combine multiple publish features:

#### Self-Contained + Ready-to-Run + Trimming

**Key Properties:**
```xml
<SelfContained>true</SelfContained>
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
<ReadyToRunEnabled>true</ReadyToRunEnabled>
<ReadyToRunUseCrossgen2>true</ReadyToRunUseCrossgen2>
<PublishTrimmed>true</PublishTrimmed>
<RequiresILLinkPack>true</RequiresILLinkPack>
```

**Expected Outputs:**
- ✅ `TargetingPacks`
- ✅ `RuntimePacks` (for the specified RID)
- ✅ `Crossgen2Packs` (for R2R compilation)
- ✅ `ImplicitPackageReferences` (ILLink for trimming)
- ✅ `PackagesToDownload` (all of the above)

#### Multi-RID + Ready-to-Run + Trimming

**Key Properties:**
```xml
<SelfContained>true</SelfContained>
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
<RuntimeIdentifiers>linux-x64;linux-arm64;win-x64</RuntimeIdentifiers>
<ReadyToRunEnabled>true</ReadyToRunEnabled>
<ReadyToRunUseCrossgen2>true</ReadyToRunUseCrossgen2>
<PublishTrimmed>true</PublishTrimmed>
<RequiresILLinkPack>true</RequiresILLinkPack>
```

**Expected Outputs:**
- ✅ `TargetingPacks`
- ✅ `RuntimePacks` (for the primary RID only)
- ✅ `Crossgen2Packs` (for the host RID)
- ✅ `ImplicitPackageReferences` (ILLink for trimming)
- ✅ `PackagesToDownload` (runtime packs for ALL RIDs, plus Crossgen2 and ILLink)

---

## Current Issues and Expected Fixes

### Issue #51667: Runtime Packs Not Resolved for PublishTrimmed/PublishAot Without SelfContained

**Problem:**
Currently, when `PublishTrimmed=true` or `PublishAot=true` is set without `SelfContained=true`, the task does not resolve runtime packs. This causes publish failures because trimming and AOT require runtime-specific assets.

**Current Behavior:**
```csharp
var runtimeRequiredByDeployment
    = (SelfContained || ReadyToRunEnabled) &&
      !string.IsNullOrEmpty(EffectiveRuntimeIdentifier) &&
      !string.IsNullOrEmpty(selectedRuntimePack?.RuntimePackNamePatterns);
```

**Expected Behavior:**
Runtime packs should be resolved when:
- `SelfContained=true`, OR
- `ReadyToRunEnabled=true`, OR
- `PublishTrimmed=true`, OR
- `PublishAot=true`

**AND** a `RuntimeIdentifier` is specified.

**Proposed Fix:**
```csharp
var runtimeRequiredByDeployment
    = (SelfContained || ReadyToRunEnabled || PublishAot || RequiresILLinkPack) &&
      !string.IsNullOrEmpty(EffectiveRuntimeIdentifier) &&
      !string.IsNullOrEmpty(selectedRuntimePack?.RuntimePackNamePatterns);
---

## Implementation Details

### Key Decision Points

The task uses the following logic to determine which packs to include:

1. **Targeting Packs**: Always included for all framework references matching the target framework
2. **Runtime Packs**: Included when `runtimeRequiredByDeployment` is true OR `RuntimePackAlwaysCopyLocal` is set
3. **Tool Packs**: Included based on specific properties:
   - Crossgen2: When `ReadyToRunEnabled && ReadyToRunUseCrossgen2`
   - ILCompiler: When `PublishAot`
   - ILLink: When `RequiresILLinkPack`

### RID Resolution

The task uses the runtime graph (`RuntimeGraphPath`) to find the best matching RID from the available runtime packs:

1. For the primary `RuntimeIdentifier`, full runtime pack metadata is generated
2. For additional `RuntimeIdentifiers`, only download entries are created
3. Portable RIDs are preferred over non-portable RIDs when appropriate for tool packs

### Version Selection

Runtime framework versions follow this precedence:
1. `RuntimeFrameworkVersion` metadata on `FrameworkReference` item
2. `RuntimeFrameworkVersion` MSBuild property
3. `LatestRuntimeFrameworkVersion` (if `TargetLatestRuntimePatch=true`)
4. `DefaultRuntimeFrameworkVersion` (if `TargetLatestRuntimePatch=false`)

---

## Testing Scenarios

The following test scenarios validate the expected outputs:

1. ✅ **Self-contained deployment** resolves runtime packs for the specified RID
2. ❌ **PublishTrimmed without SelfContained** should resolve runtime packs (currently fails)
3. ❌ **PublishAot without SelfContained** should resolve runtime packs (currently fails)
4. ✅ **Multiple RuntimeIdentifiers** downloads runtime packs for all RIDs
5. ✅ **Ready-to-Run** includes Crossgen2 packs
6. ✅ **Combined scenarios** include all necessary packs

Tests are located in: `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/ProcessFrameworkReferencesTests.cs`

---

## Related Documentation

- [Runtime Identifier Catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)
- [Framework-dependent vs Self-contained deployment](https://learn.microsoft.com/dotnet/core/deploying/)
- [Trim self-contained deployments](https://learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained)
- [Native AOT deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [ReadyToRun compilation](https://learn.microsoft.com/dotnet/core/deploying/ready-to-run)

---

## Revision History

- **2025-01-14**: Initial specification documenting expected outputs and identifying issue #51667
