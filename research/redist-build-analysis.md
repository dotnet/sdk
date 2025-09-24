# Redist Build Analysis: Why Direct Project Build Fails

## Problem Summary
Building `src/Layout/redist/redist.csproj` directly fails with multiple errors, while building through `sdk.slnx` succeeds.

## Key Errors When Building Redist Directly

### 1. Missing Assets Files (NETSDK1004)
```
error NETSDK1004: Assets file 'artifacts/obj/Sdks/Microsoft.NET.Sdk.Web/tools/project.assets.json' not found
error NETSDK1004: Assets file 'artifacts/obj/Sdks/Microsoft.NET.Sdk.Publish/tools/project.assets.json' not found
error NETSDK1004: Assets file 'artifacts/obj/Sdks/Microsoft.NET.Sdk.Worker/tools/project.assets.json' not found
```

### 2. Missing .NET Framework Reference Assemblies (MSB3644)
```
error MSB3644: The reference assemblies for .NETFramework,Version=v4.7.2 were not found
```

## Root Cause Analysis

### THE ACTUAL PROBLEM: Hidden MSBuild Dependencies

**The redist project has IMPLICIT dependencies that are NOT declared as ProjectReferences!**

In `src/Layout/redist/targets/GenerateLayout.targets:156-159`:

```xml
<ItemGroup>
  <WebSdkProjectFile Include="$(RepoRoot)src\WebSdk\**\*.csproj" />
</ItemGroup>

<MSBuild Projects="@(WebSdkProjectFile)" />
```

This target dynamically discovers and builds ALL WebSdk projects (including the ones causing errors):
- `Microsoft.NET.Sdk.Web.Tasks.csproj`
- `Microsoft.NET.Sdk.Publish.Tasks.csproj` 
- `Microsoft.NET.Sdk.Worker.Tasks.csproj`

### Why Direct Build Fails:
1. **Missing Dependencies**: WebSdk projects aren't ProjectReferences, so dotnet doesn't know to build them first
2. **Target Execution**: The `PublishNetSdks` target runs during redist build and tries to invoke MSBuild on unbuild projects
3. **Missing Assets**: WebSdk projects haven't been restored, so their `project.assets.json` files don't exist
4. **Framework Missing**: Some WebSdk projects target .NET Framework 4.7.2 which isn't installed

### Why Solution Build Works:
1. **Inclusion**: WebSdk projects ARE included in `sdk.slnx` (lines 245, 259, 271)
2. **Build Order**: Solution builds all projects in dependency order BEFORE redist
3. **Restoration**: All projects get restored through solution-level restore
4. **Available Assets**: By the time redist builds, all WebSdk projects have their assets ready

## Why Solution Build Works

1. **Dependency Resolution**: MSBuild analyzes all project references and builds dependencies first
2. **Asset Generation**: Required `project.assets.json` files are created for dependent projects
3. **Build Orchestration**: Projects are built in dependency order
4. **Multi-Target Coordination**: Outer builds complete before redist processes references

## Technical Details

### Complex Import Chain
```xml
<!-- redist.csproj imports -->
<DirectoryBuildTargetsPath>$(MSBuildThisFileDirectory)targets\Directory.Build.targets</DirectoryBuildTargetsPath>

<!-- Which imports many specialized targets -->
<Import Project="RestoreLayout.targets" />
<Import Project="BundledSdks.targets" />
<Import Project="GenerateLayout.targets" />
<!-- etc... -->
```

### Special Properties
```xml
<!-- Disables fast up-to-date check because files are deleted during build -->
<DisableFastUpToDateCheck>true</DisableFastUpToDateCheck>
<!-- Excludes build output from publish -->
<CopyBuildOutputToPublishDirectory>false</CopyBuildOutputToPublishDirectory>
```

## Workarounds for Direct Build

### Manual Pre-Build Steps
To build redist.csproj directly, you need to build the implicit dependencies first:

```bash
# Build all WebSdk projects that redist needs
dotnet build src/WebSdk/Web/Tasks/Microsoft.NET.Sdk.Web.Tasks.csproj
dotnet build src/WebSdk/Publish/Tasks/Microsoft.NET.Sdk.Publish.Tasks.csproj  
dotnet build src/WebSdk/Worker/Tasks/Microsoft.NET.Sdk.Worker.Tasks.csproj
dotnet build src/WebSdk/ProjectSystem/Tasks/Microsoft.NET.Sdk.Web.ProjectSystem.Tasks.csproj

# Build other projects discovered by globbing
dotnet build src/WebSdk/Web/Tasks/Microsoft.NET.Sdk.Web.Tasks.csproj
# ... (any other WebSdk/**/*.csproj files)

# Then build redist
dotnet build src/Layout/redist/redist.csproj
```

### Alternative: Build WebSdk Projects in Bulk
```bash
# Build all WebSdk projects at once
find src/WebSdk -name "*.csproj" -exec dotnet build {} \;

# Then build redist  
dotnet build src/Layout/redist/redist.csproj
```

### Framework Requirement
You'll also need .NET Framework 4.7.2 targeting pack installed, as some WebSdk projects multi-target to net472.

## Conclusion
The redist project has **hidden MSBuild dependencies** that aren't declared as ProjectReferences. These implicit dependencies are dynamically discovered and built during the redist build process, which fails when building redist directly because the required projects haven't been built yet.

The solution file works because it explicitly includes all these projects and builds them in dependency order before redist runs.