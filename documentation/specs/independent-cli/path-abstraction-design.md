# Path Abstraction Design for Portable .NET CLI

## Executive Summary

This design introduces a centralized `IPathResolver` abstraction that consolidates all path resolution logic in the .NET CLI. By defining **three fundamental anchor points** (Dotnet Root, SDK Root, CLI Binary), the entire system can derive all other paths from just **2 environment variables**.

---

## The Three Anchor Points

All paths in the .NET CLI ultimately derive from three baseline locations:

### 1. **DOTNET_ROOT** - Dotnet Installation Root
Where the dotnet executable and shared components live.
```
/usr/share/dotnet/
├── dotnet (executable)
├── sdk/
├── sdk-manifests/
├── packs/
├── shared/
└── host/
```

### 2. **DOTNET_SDK_ROOT** - Current SDK Tools Directory  
The versioned SDK containing CLI tools and MSBuild.
```
/usr/share/dotnet/sdk/10.0.100/
├── dotnet.dll (CLI entry point)
├── MSBuild.dll
├── Sdks/
├── DotnetTools/
└── AppHostTemplate/
```

### 3. **DOTNET_EXECUTABLE** (Process Introspection - No Config!)
The actual dotnet executable path (for self-invocation and child processes).
- **Current**: Discovered via complex Muxer logic
- **Proposed**: Use `Environment.ProcessPath` - we ARE the dotnet process!
- **Used for**: ForwardingApp, process spawning, DOTNET_HOST_PATH
- **Configuration**: **None needed** - automatically introspected from running process

---

## Current Problems

Today, the CLI makes **15+ hardcoded assumptions**:

```csharp
// Problem 1: Assumes CLI is at {dotnetRoot}/sdk/{version}/
string rootPath = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory));

// Problem 2: Scattered path construction
string msbuildPath = Path.Combine(AppContext.BaseDirectory, "MSBuild.dll");
string packsDir = Path.Combine(dotnetRoot, "packs");
string manifestsDir = Path.Combine(dotnetRoot, "sdk-manifests");
// ... repeated everywhere

// Problem 3: No single source of truth
// Each component discovers paths independently
```

---

## Proposed Solution: IPathResolver

### Interface

```csharp
public interface IPathResolver
{
    // === Anchor Points ===
    string DotnetRoot { get; }           // Where dotnet.exe lives
    string SdkRoot { get; }              // Where dotnet.dll/MSBuild.dll live
    string DotnetExecutable { get; }     // Full path to dotnet.exe
    
    // === Derived from DOTNET_ROOT ===
    string SdkDirectory { get; }         // {DOTNET_ROOT}/sdk
    string ManifestsDirectory { get; }   // {DOTNET_ROOT}/sdk-manifests
    string PacksDirectory { get; }       // {DOTNET_ROOT}/packs
    string SharedFrameworksDirectory { get; } // {DOTNET_ROOT}/shared
    
    // === Derived from SDK_ROOT ===
    string MSBuildPath { get; }          // {SDK_ROOT}/MSBuild.dll
    string MSBuildSdksPath { get; }      // {SDK_ROOT}/Sdks
    string BundledToolsDirectory { get; } // {SDK_ROOT}/DotnetTools
    string AppHostTemplateDirectory { get; } // {SDK_ROOT}/AppHostTemplate
    
    // === Helper Methods ===
    string GetBundledToolPath(string toolName);
    string GetManifestDirectory(string featureBand);
    string GetPackPath(string packId, string version, string rid);
}
```

### Implementations

#### 1. StandardLayoutPathResolver (Default)
Uses current discovery logic. No configuration needed.

```csharp
public class StandardLayoutPathResolver : IPathResolver
{
    public StandardLayoutPathResolver()
    {
        // Current behavior: discover from process/PATH/AppContext
        DotnetExecutable = DiscoverDotnetExecutable();
        DotnetRoot = Path.GetDirectoryName(DotnetExecutable)!;
        SdkRoot = AppContext.BaseDirectory;
    }
    
    public string DotnetRoot { get; }
    public string SdkRoot { get; }
    public string DotnetExecutable { get; }
    
    // All other properties derived from these three
    public string SdkDirectory => Path.Combine(DotnetRoot, "sdk");
    public string PacksDirectory => Path.Combine(DotnetRoot, "packs");
    // ...
}
```

#### 2. ConfigurablePathResolver (Portable)
Reads from environment variables. Supports relocation.

```csharp
public class ConfigurablePathResolver : IPathResolver
{
    public ConfigurablePathResolver()
    {
        // We ARE the dotnet process - use our own path (argv[0])
        DotnetExecutable = Environment.ProcessPath 
            ?? throw new InvalidOperationException("Cannot determine current process path");
        
        // User configures these two via environment variables
        DotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? Path.GetDirectoryName(DotnetExecutable)!; // Fallback: exe directory
            
        SdkRoot = Environment.GetEnvironmentVariable("DOTNET_SDK_ROOT")
            ?? AppContext.BaseDirectory; // Fallback: current SDK location
    }
    // Same derived properties
}
```

---

## User Experience

### Standard Layout (No Configuration)
```bash
# User installs SDK normally
# Everything works as today - zero configuration
dotnet build
```

### Relocated CLI (2 Environment Variables)
```bash
# User sets ONLY two variables
export DOTNET_ROOT=/opt/dotnet
export DOTNET_SDK_ROOT=/opt/dotnet/sdk/10.0.100

# System derives everything else automatically:
# - DotnetExecutable: Environment.ProcessPath (we're already running!)
# - MSBuildExtensionsPath=/opt/dotnet/sdk/10.0.100
# - MSBuildSDKsPath=/opt/dotnet/sdk/10.0.100/Sdks
# - Packs=/opt/dotnet/packs
# - Manifests=/opt/dotnet/sdk-manifests

dotnet build  # Works!
```

---

## Migration Guide: Updating Each Coupling Point

### Before & After Examples

#### Example 1: Muxer (74 usages)

**Before**:
```csharp
// Muxer.cs:41
string? rootPath = Path.GetDirectoryName(
    Path.GetDirectoryName(AppContext.BaseDirectory));
string muxerPath = Path.Combine(rootPath, $"dotnet{ExeSuffix}");
```

**After**:
```csharp
public class Muxer
{
    private readonly IPathResolver _pathResolver;
    
    public Muxer(IPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? PathResolver.Default;
    }
    
    // Simply return the current process path - we ARE dotnet!
    public string MuxerPath => _pathResolver.DotnetExecutable;
}

// In PathResolver:
public string DotnetExecutable => Environment.ProcessPath 
    ?? throw new InvalidOperationException("Cannot determine process path");
```

**Impact**: All 74 usages automatically fixed ✅

**Key Insight**: We don't need complex discovery logic - we're already running as the dotnet process!

---

#### Example 2: MSBuild Integration

**Before**:
```csharp
// MSBuildForwardingAppWithoutLogging.cs
return new Dictionary<string, string?>
{
    { "MSBuildExtensionsPath", AppContext.BaseDirectory },
    { "MSBuildSDKsPath", Path.Combine(AppContext.BaseDirectory, "Sdks") }
};
```

**After**:
```csharp
return new Dictionary<string, string?>
{
    { "MSBuildExtensionsPath", _pathResolver.SdkRoot },
    { "MSBuildSDKsPath", _pathResolver.MSBuildSdksPath }
};
```

---

#### Example 3: Bundled Tools (NuGet, Format, FSI, VSTest)

**Before**:
```csharp
// NuGetForwardingApp.cs
Path.Combine(AppContext.BaseDirectory, "NuGet.CommandLine.XPlat.dll")

// FormatForwardingApp.cs  
Path.Combine(AppContext.BaseDirectory, "DotnetTools/dotnet-format/dotnet-format.dll")
```

**After**:
```csharp
// Unified approach
_pathResolver.GetBundledToolPath("NuGet.CommandLine.XPlat")
_pathResolver.GetBundledToolPath("dotnet-format")
```

---

#### Example 4: CSharp Compiler (Packs)

**Before**:
```csharp
// CSharpCompilerCommand.cs:44-45
string SdkPath = AppContext.BaseDirectory;
string DotNetRootPath = Path.GetDirectoryName(Path.GetDirectoryName(SdkPath))!;

// Line 315
string apphostSource = Path.Join(SdkPath, "..", "..", "packs", 
    $"Microsoft.NETCore.App.Host.{rid}", RuntimeVersion, ...);
```

**After**:
```csharp
string GetAppHostPath(string rid, string version)
    => _pathResolver.GetPackPath(
        $"Microsoft.NETCore.App.Host.{rid}", 
        version, 
        rid);
```

---

#### Example 5: Workload Management

**Before**:
```csharp
// FileBasedInstaller.cs:271
Path.Combine(_workloadRootDir, "sdk-manifests", sdkFeatureBand)

// Line 104
Path.Combine(_workloadRootDir, "sdk-manifests", band, "workloadsets", version)
```

**After**:
```csharp
_pathResolver.GetManifestDirectory(sdkFeatureBand)
Path.Combine(_pathResolver.GetManifestDirectory(band), "workloadsets", version)
```

---

## Complete Refactoring Map

| Component | Current Code | Baseline Needed | Refactored Access |
|-----------|-------------|-----------------|-------------------|
| **Muxer** | `Path.GetDirectoryName(BaseDirectory) × 2` | CLI_PATH | `_pathResolver.DotnetExecutable` |
| **MSBuild Path** | `Path.Combine(BaseDirectory, "MSBuild.dll")` | SDK_ROOT | `_pathResolver.MSBuildPath` |
| **MSBuild SDKs** | `Path.Combine(BaseDirectory, "Sdks")` | SDK_ROOT | `_pathResolver.MSBuildSdksPath` |
| **NuGet Tool** | `Path.Combine(BaseDirectory, "NuGet...")` | SDK_ROOT | `_pathResolver.GetBundledToolPath("NuGet...")` |
| **Format Tool** | `Path.Combine(BaseDirectory, "DotnetTools/...")` | SDK_ROOT | `_pathResolver.GetBundledToolPath("dotnet-format")` |
| **FSI Tool** | `Path.Combine(BaseDirectory, FsiDllName)` | SDK_ROOT | `_pathResolver.GetBundledToolPath("fsi")` |
| **VSTest** | `Path.Combine(BaseDirectory, "vstest...")` | SDK_ROOT | `_pathResolver.GetBundledToolPath("vstest.console")` |
| **AppHost Template** | `Path.Combine(BaseDirectory, "AppHostTemplate")` | SDK_ROOT | `_pathResolver.AppHostTemplateDirectory` |
| **SDK Directory** | `Path.Combine(dotnetDir, "sdk")` | DOTNET_ROOT | `_pathResolver.SdkDirectory` |
| **Packs** | `Path.Combine(dotnetRoot, "packs")` | DOTNET_ROOT | `_pathResolver.PacksDirectory` |
| **Manifests** | `Path.Combine(dotnetRoot, "sdk-manifests")` | DOTNET_ROOT | `_pathResolver.ManifestsDirectory` |
| **Reference Assemblies** | `{dotnetRoot}/packs/Microsoft.NETCore.App.Ref/...` | DOTNET_ROOT | `_pathResolver.GetPackPath(...)` |

---

## Implementation Plan

### Week 1: Foundation
1. Create `IPathResolver` interface
2. Implement `StandardLayoutPathResolver`  
3. Implement `ConfigurablePathResolver`
4. Add initialization in `Program.cs`
5. Write unit tests

### Week 2-3: Core Refactoring  
1. Refactor `Muxer` class (74 usages fixed)
2. Refactor `MSBuildForwardingAppWithoutLogging`
3. Refactor all `ForwardingApp` implementations
4. Refactor `SdkInfoProvider`
5. Update environment variable setup

### Week 3-4: Advanced Features
1. Refactor `CSharpCompilerCommand`
2. Refactor workload installers
3. Refactor bundled tool paths
4. Replace all `AppContext.BaseDirectory` references

### Week 4: Testing
1. Integration tests for relocated scenarios
2. Regression tests for standard layout
3. Cross-platform validation
4. Performance benchmarks

---

## Backward Compatibility

### Existing Code (No Changes Required)
```csharp
// Old code continues to work
var muxer = new Muxer();
string path = muxer.MuxerPath;
```

### New Code (Dependency Injection)
```csharp
// New code can inject custom resolver
var resolver = new ConfigurablePathResolver();
var muxer = new Muxer(resolver);
```

### Global Default (Gradual Migration)
```csharp
// Transitional: static default for legacy code
public static class PathResolver
{
    public static IPathResolver Default { get; set; }
}

// Usage in legacy code
string msbuild = PathResolver.Default.MSBuildPath;
```

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public void ConfigurablePathResolver_UsesEnvironmentVariables()
{
    Environment.SetEnvironmentVariable("DOTNET_ROOT", "/custom/dotnet");
    Environment.SetEnvironmentVariable("DOTNET_SDK_ROOT", "/custom/sdk");
    
    var resolver = new ConfigurablePathResolver();
    
    Assert.Equal("/custom/dotnet", resolver.DotnetRoot);
    Assert.Equal("/custom/sdk", resolver.SdkRoot);
    Assert.Equal("/custom/dotnet/packs", resolver.PacksDirectory);
}
```

### Integration Tests
```csharp
[Fact]
public void RelocatedCLI_CanBuildProject()
{
    // Setup relocated environment
    SetupRelocatedLayout();
    
    // Run build
    var result = ProcessEx.Run("dotnet", "build");
    
    Assert.Equal(0, result.ExitCode);
}
```

---

## Benefits

✅ **Simple User Experience** - Just 2 environment variables  
✅ **Backward Compatible** - Standard layout unchanged  
✅ **Centralized Logic** - Single source of truth for paths  
✅ **Testable** - Easy to mock and unit test  
✅ **Maintainable** - Clear interface contracts  
✅ **Flexible** - Supports future layout changes  
✅ **Gradual Migration** - Can refactor incrementally  

---

## Risk Mitigation

### Low Risk
- ✅ No breaking changes to public API
- ✅ Standard layout behavior unchanged
- ✅ Incremental refactoring possible
- ✅ Comprehensive test coverage

### Managed Risks
- ⚠️ Performance: Use lazy evaluation/caching
- ⚠️ Thread safety: Make resolver immutable
- ⚠️ Discovery failures: Fallback to standard logic

---

## Success Criteria

1. ✅ User can set `DOTNET_ROOT` + `DOTNET_SDK_ROOT` and CLI works
2. ✅ Zero regressions in standard layout scenarios
3. ✅ All 15+ coupling points refactored
4. ✅ 95%+ test coverage of path resolution
5. ✅ Documentation for users and developers
6. ✅ Performance within 5% of baseline

---

## Next Steps

1. **Review this design** with .NET SDK team
2. **Prototype `IPathResolver`** interface
3. **Pilot refactoring** with Muxer class
4. **Validate approach** with integration tests
5. **Iterate based on feedback**
