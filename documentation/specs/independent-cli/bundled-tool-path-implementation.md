# GetBundledToolPath Implementation Details

## Current Tool Layout Patterns

Based on the actual code, bundled tools have **three different layout patterns**:

### Pattern 1: Direct in SDK Root
- **NuGet**: `{SdkRoot}/NuGet.CommandLine.XPlat.dll`
- **VSTest**: `{SdkRoot}/vstest.console.dll`

### Pattern 2: DotnetTools Subdirectory with Tool Name
- **Format**: `{SdkRoot}/DotnetTools/dotnet-format/dotnet-format.dll`
  - Also has: `.deps.json`, `.runtimeconfig.json`

### Pattern 3: Direct Subdirectory
- **FSI**: `{SdkRoot}/FSharp/fsi.dll`

---

## Proposed Implementation

### Option A: Simple String-Based (Explicit Paths)

```csharp
public string GetBundledToolPath(string relativePath)
{
    // Caller specifies the exact relative path from SDK root
    return Path.Combine(SdkRoot, relativePath);
}

// Usage:
_pathResolver.GetBundledToolPath("NuGet.CommandLine.XPlat.dll")
_pathResolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.dll")
_pathResolver.GetBundledToolPath("vstest.console.dll")
_pathResolver.GetBundledToolPath("FSharp/fsi.dll")
```

**Pros**: Simple, flexible, explicit
**Cons**: Callers need to know the exact path structure

---

### Option B: Tool Name with Layout Detection (Smarter)

```csharp
public interface IPathResolver
{
    // Main method - tries to locate tool intelligently
    string GetBundledToolPath(string toolName);
    
    // Explicit overloads for specific patterns
    string GetBundledToolInRoot(string dllFileName);
    string GetBundledToolInSubdirectory(string subdirectory, string toolName);
}

public class StandardLayoutPathResolver : IPathResolver
{
    public string GetBundledToolPath(string toolName)
    {
        // Well-known tools with explicit mappings
        return toolName switch
        {
            "nuget" or "NuGet.CommandLine.XPlat" => 
                Path.Combine(SdkRoot, "NuGet.CommandLine.XPlat.dll"),
                
            "vstest" or "vstest.console" => 
                Path.Combine(SdkRoot, "vstest.console.dll"),
                
            "dotnet-format" => 
                Path.Combine(SdkRoot, "DotnetTools", "dotnet-format", "dotnet-format.dll"),
                
            "fsi" => 
                Path.Combine(SdkRoot, "FSharp", "fsi.dll"),
                
            _ => throw new ArgumentException($"Unknown bundled tool: {toolName}")
        };
    }
    
    public string GetBundledToolInRoot(string dllFileName)
    {
        return Path.Combine(SdkRoot, dllFileName);
    }
    
    public string GetBundledToolInSubdirectory(string subdirectory, string toolName)
    {
        return Path.Combine(SdkRoot, subdirectory, toolName, $"{toolName}.dll");
    }
}
```

**Pros**: Type-safe, discoverable, handles known tools
**Cons**: Need to maintain tool registry, less flexible for new tools

---

### Option C: Hybrid Approach (Recommended)

```csharp
public interface IPathResolver
{
    /// <summary>
    /// Gets path to a bundled tool using relative path from SDK root.
    /// </summary>
    /// <param name="relativePath">Path relative to SDK root, e.g., "NuGet.CommandLine.XPlat.dll"</param>
    string GetBundledToolPath(string relativePath);
    
    /// <summary>
    /// Gets path to a well-known bundled tool by name.
    /// </summary>
    /// <param name="toolName">Tool name: nuget, vstest, dotnet-format, fsi</param>
    string GetWellKnownToolPath(WellKnownTool toolName);
}

public enum WellKnownTool
{
    NuGet,
    VSTest,
    Format,
    FSI
}

public class StandardLayoutPathResolver : IPathResolver
{
    // Simple pass-through for explicit paths
    public string GetBundledToolPath(string relativePath)
    {
        return Path.Combine(SdkRoot, relativePath);
    }
    
    // Explicit mapping for well-known tools
    public string GetWellKnownToolPath(WellKnownTool toolName)
    {
        return toolName switch
        {
            WellKnownTool.NuGet => 
                Path.Combine(SdkRoot, "NuGet.CommandLine.XPlat.dll"),
                
            WellKnownTool.VSTest => 
                Path.Combine(SdkRoot, "vstest.console.dll"),
                
            WellKnownTool.Format => 
                Path.Combine(SdkRoot, "DotnetTools", "dotnet-format", "dotnet-format.dll"),
                
            WellKnownTool.FSI => 
                Path.Combine(SdkRoot, "FSharp", "fsi.dll"),
                
            _ => throw new ArgumentOutOfRangeException(nameof(toolName))
        };
    }
}
```

**Pros**: Best of both worlds - type-safe for known tools, flexible for new ones
**Cons**: Slightly more API surface

---

## Recommended: Option C with Helper Extensions

### Full Implementation

```csharp
namespace Microsoft.DotNet.Cli.Utils;

public interface IPathResolver
{
    // === Core Paths ===
    string DotnetRoot { get; }
    string SdkRoot { get; }
    string DotnetExecutable { get; }
    
    // === Bundled Tools ===
    
    /// <summary>
    /// Gets the full path to a bundled tool using relative path from SDK root.
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to SDK root. Examples:
    /// - "NuGet.CommandLine.XPlat.dll"
    /// - "DotnetTools/dotnet-format/dotnet-format.dll"
    /// - "FSharp/fsi.dll"
    /// </param>
    string GetBundledToolPath(string relativePath);
}

// Extension methods for convenience
public static class PathResolverExtensions
{
    public static string GetNuGetPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("NuGet.CommandLine.XPlat.dll");
    
    public static string GetVSTestPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("vstest.console.dll");
    
    public static string GetFormatPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.dll");
    
    public static string GetFormatDepsPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.deps.json");
    
    public static string GetFormatRuntimeConfigPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.runtimeconfig.json");
    
    public static string GetFsiPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("FSharp/fsi.dll");
}

public class StandardLayoutPathResolver : IPathResolver
{
    // ... other properties ...
    
    public string GetBundledToolPath(string relativePath)
    {
        return Path.Combine(SdkRoot, relativePath);
    }
}
```

---

## Usage Examples

### Before (Current Code)

```csharp
// NuGetForwardingApp.cs
private static string GetNuGetExePath()
{
    return Path.Combine(AppContext.BaseDirectory, "NuGet.CommandLine.XPlat.dll");
}

// FormatForwardingApp.cs
private static string GetForwardApplicationPath()
    => Path.Combine(AppContext.BaseDirectory, "DotnetTools/dotnet-format/dotnet-format.dll");

// VSTestForwardingApp.cs
return Path.Combine(AppContext.BaseDirectory, VstestAppName);

// FsiForwardingApp.cs
var dllPath = Path.Combine(AppContext.BaseDirectory, FsiDllName);
```

### After (With IPathResolver)

```csharp
// NuGetForwardingApp.cs
public class NuGetForwardingApp
{
    private readonly IPathResolver _pathResolver;
    
    private string GetNuGetExePath()
    {
        return _pathResolver.GetNuGetPath();
        // Or: _pathResolver.GetBundledToolPath("NuGet.CommandLine.XPlat.dll");
    }
}

// FormatForwardingApp.cs
public class FormatForwardingApp : ForwardingApp
{
    public FormatForwardingApp(IEnumerable<string> argsToForward, IPathResolver? pathResolver = null)
        : base(
            forwardApplicationPath: (pathResolver ?? PathResolver.Default).GetFormatPath(),
            argsToForward,
            depsFile: (pathResolver ?? PathResolver.Default).GetFormatDepsPath(),
            runtimeConfig: (pathResolver ?? PathResolver.Default).GetFormatRuntimeConfigPath())
    {
    }
}

// VSTestForwardingApp.cs
private string GetVSTestExePath()
{
    string? override = Environment.GetEnvironmentVariable("VSTEST_CONSOLE_PATH");
    if (!string.IsNullOrWhiteSpace(override))
        return override;
    
    return _pathResolver.GetVSTestPath();
}

// FsiForwardingApp.cs
var dllPath = _pathResolver.GetFsiPath();
```

---

## Alternative: Tool Manifest (Future Enhancement)

For even more flexibility, you could add a tool manifest:

```csharp
// tools.json in SDK root
{
  "bundledTools": {
    "nuget": {
      "path": "NuGet.CommandLine.XPlat.dll"
    },
    "vstest": {
      "path": "vstest.console.dll"
    },
    "dotnet-format": {
      "path": "DotnetTools/dotnet-format/dotnet-format.dll",
      "deps": "DotnetTools/dotnet-format/dotnet-format.deps.json",
      "runtimeConfig": "DotnetTools/dotnet-format/dotnet-format.runtimeconfig.json"
    },
    "fsi": {
      "path": "FSharp/fsi.dll"
    }
  }
}

// Then:
public string GetBundledToolPath(string toolName)
{
    var manifest = LoadToolManifest(); // Cached
    if (manifest.TryGetValue(toolName, out var toolInfo))
    {
        return Path.Combine(SdkRoot, toolInfo.Path);
    }
    
    throw new ArgumentException($"Unknown tool: {toolName}");
}
```

---

## Recommendation

**Use Option C (Hybrid) with Extension Methods**:

1. **Simple implementation**: `GetBundledToolPath(relativePath)` for flexibility
2. **Extension methods**: Type-safe helpers for well-known tools
3. **Future-proof**: Easy to add new tools without changing interface
4. **Backward compatible**: Can support tool manifest later if needed

### Final Interface:

```csharp
public interface IPathResolver
{
    string DotnetRoot { get; }
    string SdkRoot { get; }
    string DotnetExecutable { get; }
    
    // Simple and flexible
    string GetBundledToolPath(string relativePath);
    
    // Derived paths
    string MSBuildPath { get; }
    string MSBuildSdksPath { get; }
    // ... etc
}

// Type-safe convenience via extensions
public static class PathResolverExtensions
{
    public static string GetNuGetPath(this IPathResolver resolver)
        => resolver.GetBundledToolPath("NuGet.CommandLine.XPlat.dll");
    
    // ... etc
}
```

This keeps the interface clean while providing type-safety and discoverability for well-known tools!
