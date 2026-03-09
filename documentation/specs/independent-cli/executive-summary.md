# .NET CLI Portability Analysis - Executive Summary

## Investigation Completed: January 22, 2026

### Objective
Analyze how tightly coupled the dotnet CLI is to the .NET SDK/Runtime layout to determine feasibility of making the CLI portable/relocatable (movable independently of the SDK).

---

## Key Findings

### Current State: **TIGHTLY COUPLED**

The .NET CLI has **deep architectural assumptions** about being located at `{dotnetRoot}/sdk/{version}/`. Moving the CLI executable without the entire SDK breaks major functionality.

### Critical Coupling Points Identified: **15+**

1. **Muxer Path Discovery** (CRITICAL)
   - Assumes CLI is 2 directories below dotnet root
   - Used in 74+ locations throughout codebase
   
2. **MSBuild Integration** (CRITICAL)
   - MSBuild.dll must be in same directory as CLI
   - MSBuildExtensionsPath set to CLI directory
   - MSBuildSDKsPath set to `{CLI}/Sdks`
   
3. **Hostfxr Native Dependency** (CRITICAL)
   - P/Invoke to native SDK resolver library
   - Expects hostfxr in standard .NET layout
   
4. **CSharp Compiler (dotnet run)** (CRITICAL)
   - Reference assemblies: `{dotnetRoot}/packs/Microsoft.NETCore.App.Ref/`
   - AppHost template: `{dotnetRoot}/packs/Microsoft.NETCore.App.Host.{rid}/`
   
5. **Workload System** (HIGH)
   - Manifests: `{dotnetRoot}/sdk-manifests/`
   - Packs: `{dotnetRoot}/packs/`

6. **Bundled Tools** (MEDIUM)
   - Format, FSI, VSTest, NuGet all in `{CLI Directory}`

### Environment Variables Cataloged: **30+**

Including critical override mechanisms:
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` - Override dotnet directory
- `DOTNET_HOST_PATH` - Override dotnet executable path  
- `MSBuildExtensionsPath`, `MSBuildSDKsPath` - MSBuild paths
- `DOTNETSDK_WORKLOAD_PACK_ROOTS` - Workload pack locations

---

## Answer: Can the CLI be Made Portable?

### **YES, with caveats**

**Feasibility by Approach**:

#### ✅ Option 1: Environment Variables (Available Today)
- **Effort**: None (no code changes)
- **User Complexity**: High (~10 env vars to set)
- **Use Case**: Testing, CI environments, advanced users

#### ✅ Option 2: Wrapper Script (Recommended for POC)
- **Effort**: Low (scripting only)
- **User Complexity**: Low (wrapper handles env vars)
- **Use Case**: Distribution packages, controlled environments

#### ⚠️ Option 3: Configuration File (Best Long-Term)
- **Effort**: Medium (2-4 weeks engineering)
- **User Complexity**: Low (single config file)
- **Use Case**: Official portability support
- **Requires**: Code changes to introduce `IPathResolver` abstraction

#### ⚠️ Option 4: Standalone Bundle (Major Effort)
- **Effort**: High (2-3 months engineering)
- **User Complexity**: None (self-contained)
- **Use Case**: Complete independence from SDK
- **Requires**: Major refactoring of path resolution throughout codebase

---

## Impact Analysis by Feature

| Feature | Works When Relocated? | Workaround Available? |
|---------|----------------------|----------------------|
| `dotnet build/restore/publish` | ❌ Breaks | ✅ Environment variables |
| `dotnet test` | ❌ Breaks | ✅ Environment variables |
| `dotnet run` | ❌ Breaks | ✅ Partial (not file.cs) |
| `dotnet workload` | ❌ Breaks | ✅ Environment variables |
| `dotnet tool install` (global) | ✅ Works | N/A (uses user profile) |
| `dotnet --info` | ✅ Works | N/A |
| `dotnet new` | ❌ Breaks | ✅ Environment variables |

---

## Recommendations

### For Immediate Needs (This Week)
1. **Test with environment variables** - Validate approach
2. **Create wrapper script** - Distribution-ready solution
3. **Document required configuration** - Share with stakeholders

### For Long-Term Support (This Quarter)
1. **Propose configuration file support** to .NET SDK team
2. **Design `IPathResolver` abstraction**
3. **Create prototype** with minimal code changes
4. **Add integration tests** for relocated CLI scenarios

### For Production Use (6+ Months)
1. **Implement Option 3** (configuration file)
2. **Comprehensive testing** across all commands
3. **Documentation** for users and SDK developers
4. **Backward compatibility** validation

---

## Deliverables

1. ✅ **Coupling Catalog** - 20 sections, 15+ critical coupling points
2. ✅ **Environment Variable Catalog** - 30+ variables documented  
3. ✅ **Portability Assessment** - 4 options with effort estimates
4. ✅ **Risk Analysis** - Impact by feature area
5. ✅ **Implementation Roadmap** - Phased approach

Full documentation: `coupling-catalog.md` (28KB, comprehensive analysis)

---

## Bottom Line

**The .NET CLI CAN be made portable**, but it requires either:
- **Short-term**: User configuration via environment variables/wrapper scripts
- **Long-term**: Code changes to support configurable path resolution

The current architecture is **intentionally coupled** to the SDK layout for simplicity and performance. Portability is achievable but was not a design goal of the original implementation.

**Recommended next step**: Create a proof-of-concept wrapper script and test with realistic scenarios to validate the environment variable approach before proposing code changes.
