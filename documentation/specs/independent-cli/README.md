# Independent CLI Specification

This directory contains the specification and design documents for making the .NET CLI portable/relocatable (independent of the SDK/Runtime layout).

## Overview

The .NET CLI is currently tightly coupled to the SDK/Runtime installation layout, assuming it lives at `{dotnetRoot}/sdk/{version}/`. This specification explores how to decouple the CLI from this layout to enable portable deployments.

## Documents

### 📊 Analysis & Assessment

1. **[executive-summary.md](executive-summary.md)** - High-level findings and recommendations
   - Current state assessment
   - Feasibility analysis
   - Impact by feature area
   - Recommended approaches

2. **[coupling-catalog.md](coupling-catalog.md)** - Comprehensive technical analysis
   - 15+ critical coupling points documented
   - 30+ environment variables cataloged
   - Detailed impact analysis by component
   - Complete path assumption inventory

### 🏗️ Architecture & Design

3. **[path-abstraction-design.md](path-abstraction-design.md)** - Core abstraction design
   - The three anchor points (DOTNET_ROOT, SDK_ROOT, executable)
   - `IPathResolver` interface specification
   - Migration strategy for each coupling point
   - Implementation plan and timeline

4. **[bundled-tool-path-implementation.md](bundled-tool-path-implementation.md)** - Implementation details
   - Bundled tool layout patterns
   - `GetBundledToolPath` implementation options
   - Usage examples and migration guide

## Key Findings

### Current State
The .NET CLI makes **15+ hardcoded assumptions** about being located at `{dotnetRoot}/sdk/{version}/`:
- Muxer discovery (74+ usages)
- MSBuild integration paths
- Bundled tool locations
- Workload pack directories
- Reference assembly paths

### Proposed Solution
Introduce `IPathResolver` abstraction with **three anchor points**:
1. **DOTNET_ROOT** - Where dotnet.exe and shared components live
2. **DOTNET_SDK_ROOT** - Where the versioned SDK tools live
3. **DotnetExecutable** - Auto-discovered via `Environment.ProcessPath`

### User Experience
Users can relocate the CLI by setting just **2 environment variables**:
```bash
export DOTNET_ROOT=/opt/dotnet
export DOTNET_SDK_ROOT=/opt/dotnet/sdk/10.0.100
```

All other paths are automatically derived from these two baselines.

## Implementation Status

- ✅ **Analysis Complete** - All coupling points cataloged
- ✅ **Design Complete** - Architecture documented
- ✅ **Implementation Complete** - PathResolver applied throughout codebase (38 files)
- ✅ **MSBuild Fix Complete** - Assembly loading issue resolved
- ⏳ **Testing** - Ready for testing

### Recent Updates (January 23, 2026)

**Path Abstraction**: Fully implemented and integrated
- Created `IPathResolver` interface with 3 anchor properties
- Implemented `StandardLayoutPathResolver` and `ConfigurablePathResolver`
- Refactored 35+ files to use PathResolver
- All builds passing

**MSBuild Assembly Loading Fix**: Critical issue resolved
- Identified: MSBuild uses `Assembly.Location` internally
- Solution 1: Added `MSBUILD_EXE_PATH` environment variable
- Solution 2: Integrated `Microsoft.Build.Locator` for in-process usage
- Documented in [msbuild-assembly-loading-analysis.md](msbuild-assembly-loading-analysis.md)

## Next Steps

1. **Review** - Get feedback from SDK team on design
2. **Prototype** - Implement `IPathResolver` interface
3. **Pilot** - Refactor Muxer class as proof of concept
4. **Validate** - Test with environment variables
5. **Iterate** - Refactor remaining coupling points

## Related Issues

- **Goal**: Make .NET CLI portable/relocatable
- **Use Cases**: 
  - Containerization
  - Alternative distribution models
  - Standalone CLI deployments
  - Custom SDK layouts

## Authors

This specification was created as part of the SDK portability investigation.

**Investigation Date**: January 22-23, 2026

## Additional Resources

- SDK codebase: `src/Cli/`
- Native SDK resolver: `src/Resolvers/Microsoft.DotNet.NativeWrapper/`
- Environment variables: `src/Common/EnvironmentVariableNames.cs`
