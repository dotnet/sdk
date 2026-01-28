# MSBuild Assembly Loading Analysis - CRITICAL ISSUE IDENTIFIED

## Executive Summary

**🚨 YOU ARE CORRECT**: MSBuild's internal implementation DOES use Assembly.Location to compute paths, creating a critical issue for portable CLI scenarios.

## The Problem

MSBuild uses BuildEnvironmentHelper which includes TryFromMSBuildAssembly():

```csharp
private static BuildEnvironment TryFromMSBuildAssembly()
{
    var buildAssembly = s_getExecutingAssemblyPath();  // Gets MSBuild.dll location!
    var msBuildDll = Path.Combine(FileUtilities.GetFolderAbove(buildAssembly), "MSBuild.dll");
    // Computes paths relative to where MSBuild.dll is loaded from
}
```

## Impact: Portable Scenario Breaks

If MSBuild.dll loads from `/opt/custom-cli/MSBuild.dll` but SDK files are at `/usr/share/dotnet/sdk/10.0.100/`, MSBuild will look for Microsoft.Common.targets at the WRONG location.

## SOLUTION: Set MSBUILD_EXE_PATH

MSBuild checks MSBUILD_EXE_PATH FIRST (before assembly location):

```csharp
private static BuildEnvironment TryFromEnvironmentVariable()
{
    var msBuildExePath = s_getEnvironmentVariable("MSBUILD_EXE_PATH");
    // This takes priority over assembly location!
}
```

**Fix**: Add to MSBuildForwardingAppWithoutLogging.cs:

```csharp
internal static Dictionary<string, string?> GetMSBuildRequiredEnvironmentVariables()
{
    var pathResolver = PathResolver.Default;
    return new()
    {
        { "MSBuildExtensionsPath", ... },
        { "MSBuildSDKsPath", ... },
        { "DOTNET_HOST_PATH", ... },
        { "MSBUILD_EXE_PATH", pathResolver.GetMSBuildPath() },  // ADD THIS
    };
}
```

This forces MSBuild to use the SDK_ROOT location regardless of where its assembly loaded from.
