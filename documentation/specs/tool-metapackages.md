# .NET Tool Metapackages Design

**Related Issue:** [#52609](https://github.com/dotnet/sdk/issues/52609)

## Overview

This document describes the design and implementation of .NET tool metapackages, which enable multiple related tools to be installed with a single gesture. This feature is inspired by VS Code's Extension Packs and addresses common scenarios where users need to install multiple related tools.

## Motivation

Users often need to install multiple related .NET tools together. Today, each tool must be installed individually, which:
- Increases cognitive load when discovering tools
- Requires multiple commands with repeated help text about PATH configuration
- Makes common scenarios (like installing all diagnostic tools) unnecessarily complex
- Is particularly painful in Dockerfiles where tools like dotnet-dump, dotnet-trace, dotnet-counters, etc. are installed one at a time

### Last Known Good (LKG) Design Philosophy

This design uses a "Last Known Good" approach where metapackages specify exact versions of tools that have been tested to work well together. This provides:

**Safety and Predictability:**
- Users get a tested, stable set of tools guaranteed to work together
- No surprises from automatic version resolution or updates
- Reproducible installations across different environments

**Quality Control:**
- Individual tools can evolve independently with varying quality/testing levels
- Metapackage authors explicitly choose which versions are "production ready"
- Pre-release or experimental tool versions won't automatically be included

**Uniform Experience:**
- Tools with shared formats (e.g., diagnostic tools sharing trace formats) are guaranteed to be compatible
- Version mismatches between related tools are prevented
- Users get a cohesive toolset with consistent behavior

## Use Cases

This feature targets three primary scenarios:

### 1. AI Tools Metapackage
```bash
dotnet tool install -g dotnet-ai-tools
```
This would install:
- `dotnet-ai-cli` - AI command-line interface
- `dotnet-ai-mcp` - AI Model Context Protocol server

### 2. Diagnostic Tools Metapackage
```bash
dotnet tool install -g dotnet-diagnostics
```
This would install:
- `dotnet-dump` - Dump collection and analysis tool
- `dotnet-trace` - Performance tracing tool
- `dotnet-counters` - Performance counter monitoring tool
- `dotnet-gcdump` - GC dump collection tool
- `dotnet-stack` - Stack trace reporting tool
- `dotnet-sos` - SOS debugging extension installer

### 3. Essential SDK Tools Metapackage
```bash
dotnet tool install -g dotnet-essential-tools
```
This could enable removing rarely-used tools from the SDK itself while making them easily discoverable and installable together.

## File Format Changes

### DotnetToolSettings.xml Schema Extension

The existing `DotNetCliTool` XML schema will be extended to support metapackages:

**Current Schema (for reference):**
```xml
<?xml version="1.0" encoding="utf-8" ?>
<DotNetCliTool Version="2">
  <Commands>
    <Command Name="dotnet-dump" EntryPoint="dotnet-dump" Runner="executable" />
  </Commands>
  <RuntimeIdentifierPackages>
    <RuntimeIdentifierPackage RuntimeIdentifier="win-x64" Id="dotnet-dump.win-x64" />
    <RuntimeIdentifierPackage RuntimeIdentifier="linux-x64" Id="dotnet-dump.linux-x64" />
  </RuntimeIdentifierPackages>
</DotNetCliTool>
```

**New Metapackage Schema (Last Known Good):**
```xml
<?xml version="1.0" encoding="utf-8" ?>
<DotNetCliTool Version="3" IsMetapackage="true">
  <Commands>
    <!-- Empty for metapackages - no direct commands -->
  </Commands>
  <MetapackageTools>
    <Tool Id="dotnet-dump" Version="8.0.553" />
    <Tool Id="dotnet-trace" Version="8.0.553" />
    <Tool Id="dotnet-counters" Version="8.0.553" />
    <Tool Id="dotnet-gcdump" Version="8.0.553" />
    <Tool Id="dotnet-stack" Version="8.0.553" />
    <Tool Id="dotnet-sos" Version="8.0.553" />
  </MetapackageTools>
</DotNetCliTool>
```

**Key Schema Changes:**
- `Version="3"` - New version for metapackage support
- `IsMetapackage="true"` - Attribute to identify metapackages
- `<MetapackageTools>` - New element containing tool references
- `<Tool>` - Each tool reference with `Id` and exact `Version` attributes (Last Known Good)
- `Commands` element is empty for metapackages (no direct executable)

**Last Known Good (LKG) Design:**
Metapackages use exact versions (not version ranges) to specify the "last known good" set of tools that have been tested to work well together. This approach:
- Ensures predictable, tested tool combinations
- Allows individual tools to evolve independently with different quality/testing levels
- Guarantees tools with shared formats are updated together for a uniform experience
- Provides a safer default than open-ended version ranges

### NuGet Package Structure

Metapackages follow the same basic structure as regular tool packages:

```
dotnet-diagnostics.1.0.0.nupkg
├── tools/
│   └── net8.0/
│       └── any/
│           └── DotnetToolSettings.xml
├── dotnet-diagnostics.nuspec
└── [Content_Types].xml
```

**Key differences from regular tool packages:**
- No executable binaries in the package
- `DotnetToolSettings.xml` contains metapackage definition
- Package type remains `DotnetTool`
- Dependencies section lists the included tools as NuGet dependencies (optional, for package management clarity)

## Implementation Details

### Code Changes Required

#### 1. Configuration Deserialization (`src/Cli/dotnet/ToolPackage/ToolConfigurationDeserialization/`)

**New file: `DotNetCliToolMetapackage.cs`**
```csharp
namespace Microsoft.DotNet.Cli.ToolPackage.ToolConfigurationDeserialization;

[Serializable]
[DebuggerStepThrough]
[XmlType(AnonymousType = true)]
public class DotNetCliToolMetapackageTool
{
    [XmlAttribute]
    public string? Id { get; set; }

    [XmlAttribute]
    public string? Version { get; set; }
}
```

**Update: `DotNetCliTool.cs`**
```csharp
[DebuggerStepThrough]
[XmlRoot(Namespace = "", IsNullable = false)]
public class DotNetCliTool
{
    [XmlArrayItem("Command", IsNullable = false)]
    public DotNetCliToolCommand[] Commands { get; set; }

    [XmlArrayItem("RuntimeIdentifierPackage", IsNullable = false)]
    public DotNetCliToolRuntimeIdentifierPackage[] RuntimeIdentifierPackages { get; set; }

    [XmlArrayItem("Tool", IsNullable = false)]
    public DotNetCliToolMetapackageTool[] MetapackageTools { get; set; }

    [XmlAttribute(AttributeName = "Version")]
    public string Version { get; set; }

    [XmlAttribute(AttributeName = "IsMetapackage")]
    public bool IsMetapackage { get; set; }
}
```

#### 2. Tool Configuration (`src/Cli/dotnet/ToolPackage/ToolConfiguration.cs`)

Add metapackage support:
```csharp
public class ToolConfiguration
{
    // Existing properties
    public string CommandName { get; set; }
    public string ToolAssemblyEntryPoint { get; set; }
    public IDictionary<string, PackageIdentity> RidSpecificPackages { get; set; }
    
    // New properties for metapackages
    public bool IsMetapackage { get; set; }
    public IReadOnlyList<MetapackageToolReference> MetapackageTools { get; set; }
}

public class MetapackageToolReference
{
    public string Id { get; set; }
    public NuGetVersion Version { get; set; }
}
```

#### 3. Tool Installation Command (`src/Cli/dotnet/Commands/Tool/Install/ToolInstallGlobalOrToolPathCommand.cs`)

**Key changes:**
- Detect metapackages during package inspection
- Install each tool in the metapackage sequentially
- Track installation success/failure for each tool
- Print consolidated success message with all installed tools
- Show PATH configuration help **only once** at the end

**Pseudocode logic:**
```csharp
private int InstallTool()
{
    var toolPackage = DownloadAndExtractPackage();
    
    if (toolPackage.Configuration.IsMetapackage)
    {
        return InstallMetapackage(toolPackage);
    }
    else
    {
        return InstallSingleTool(toolPackage);
    }
}

private int InstallMetapackage(IToolPackage metapackage)
{
    var installedTools = new List<InstalledToolInfo>();
    var failedTools = new List<(string Id, string Error)>();
    bool isFirstInstall = !AnyToolsAlreadyInstalled();
    
    foreach (var toolRef in metapackage.Configuration.MetapackageTools)
    {
        try
        {
            // Install the exact LKG version specified in the metapackage
            var tool = InstallToolFromMetapackage(toolRef.Id, toolRef.Version);
            installedTools.Add(new InstalledToolInfo(
                tool.Command.Name, 
                tool.Id, 
                tool.Version.ToNormalizedString()));
        }
        catch (Exception ex)
        {
            failedTools.Add((toolRef.Id, ex.Message));
        }
    }
    
    // Print consolidated success message
    PrintMetapackageSuccessMessage(
        metapackage.Id, 
        installedTools, 
        failedTools,
        isFirstInstall);
    
    return failedTools.Any() ? 1 : 0;
}

private void PrintMetapackageSuccessMessage(
    string metapackageId,
    List<InstalledToolInfo> installed,
    List<(string, string)> failed,
    bool showPathHelp)
{
    if (installed.Any())
    {
        _reporter.WriteLine(
            string.Format(
                CliCommandStrings.MetapackageInstallSucceeded,
                metapackageId,
                installed.Count).Green());
        
        _reporter.WriteLine(CliCommandStrings.MetapackageInstalledToolsHeader);
        foreach (var tool in installed)
        {
            _reporter.WriteLine($"  - {tool.CommandName} ({tool.PackageId} v{tool.Version})");
        }
        
        // Show PATH help only once, and only for first-time installs
        if (showPathHelp && _toolPathOptionValue == null)
        {
            _reporter.WriteLine();
            _reporter.WriteLine(CliCommandStrings.ToolInstallPathHelp);
        }
    }
    
    if (failed.Any())
    {
        _reporter.WriteLine();
        _reporter.WriteLine(
            string.Format(
                CliCommandStrings.MetapackageInstallPartialFailure,
                failed.Count).Red());
        
        foreach (var (id, error) in failed)
        {
            _reporter.WriteLine($"  - {id}: {error}");
        }
    }
}
```

#### 4. Resource Strings (`src/Cli/dotnet/Commands/CliCommandStrings.resx`)

New resource strings needed:
```xml
<data name="MetapackageInstallSucceeded" xml:space="preserve">
  <value>Successfully installed metapackage '{0}' with {1} tool(s).</value>
</data>
<data name="MetapackageInstalledToolsHeader" xml:space="preserve">
  <value>The following tools are now available:</value>
</data>
<data name="MetapackageInstallPartialFailure" xml:space="preserve">
  <value>Warning: {0} tool(s) failed to install.</value>
</data>
<data name="ToolInstallPathHelp" xml:space="preserve">
  <value>You can invoke these tools from the shell by typing their command names. You may need to restart your shell or add the tools directory to your PATH environment variable.</value>
</data>
<data name="MetapackageCannotUseRidSpecific" xml:space="preserve">
  <value>Metapackages cannot be RID-specific. Package '{0}' is invalid.</value>
</data>
```

### RID-Specific Tools and Metapackages

**Design Decision: Metapackages cannot be RID-specific**

**Rationale:**
1. Metapackages reference other tool packages by ID and version range
2. Each referenced tool can independently be RID-specific if needed
3. The RID resolution happens at the individual tool level during installation
4. This keeps the metapackage portable across platforms
5. Platform-specific metapackages would add unnecessary complexity

**Implementation:**
- Validation check: If `IsMetapackage="true"` AND `RuntimeIdentifierPackages` is present → Error
- Error message: "Metapackages cannot be RID-specific. Package '{0}' is invalid."
- The check happens in `ToolConfigurationDeserializer.cs`

**Example validation code:**
```csharp
if (dotNetCliTool.IsMetapackage && 
    dotNetCliTool.RuntimeIdentifierPackages?.Length > 0)
{
    throw new ToolConfigurationException(
        string.Format(
            CliCommandStrings.MetapackageCannotUseRidSpecific,
            packageId));
}
```

### Local Installation (dnx) and Metapackages

**Design Decision: Metapackages are only supported for global installation**

**Rationale:**
1. Local tool manifests track specific tool versions for reproducible builds
2. Metapackages reference other packages, adding indirection to the manifest
3. The primary use case (e.g., installing diagnostic tools in containers) is global installation
4. Users who need specific versions should install individual tools locally with exact versions

**Implementation:**
- When installing with `--local` or when a tool manifest exists: Reject metapackages
- Error message: "Metapackages are only supported for global installation. Use 'dotnet tool install -g' or install individual tools locally."

**Validation code location:** `ToolInstallLocalCommand.cs`
```csharp
private void ValidateNotMetapackage(IToolPackage package)
{
    if (package.Configuration.IsMetapackage)
    {
        throw new GracefulException(
            CliCommandStrings.MetapackageNotSupportedForLocalInstall);
    }
}
```

### Output Examples

#### Installing a metapackage (first time installing any tools):
```
$ dotnet tool install -g dotnet-diagnostics
Successfully installed metapackage 'dotnet-diagnostics' with 6 tool(s).

The following tools are now available:
  - dotnet-dump (dotnet-dump v8.0.1)
  - dotnet-trace (dotnet-trace v8.0.1)
  - dotnet-counters (dotnet-counters v8.0.1)
  - dotnet-gcdump (dotnet-gcdump v8.0.1)
  - dotnet-stack (dotnet-stack v8.0.1)
  - dotnet-sos (dotnet-sos v8.0.1)

You can invoke these tools from the shell by typing their command names. 
You may need to restart your shell or add the tools directory to your PATH environment variable.
```

#### Installing a metapackage (tools already installed):
```
$ dotnet tool install -g dotnet-ai-tools
Successfully installed metapackage 'dotnet-ai-tools' with 2 tool(s).

The following tools are now available:
  - dotnet-ai-cli (dotnet-ai-cli v1.0.0)
  - dotnet-ai-mcp (dotnet-ai-mcp v1.0.0)
```

#### Installing a metapackage with partial failure:
```
$ dotnet tool install -g dotnet-diagnostics
Successfully installed metapackage 'dotnet-diagnostics' with 5 tool(s).

The following tools are now available:
  - dotnet-dump (dotnet-dump v8.0.1)
  - dotnet-trace (dotnet-trace v8.0.1)
  - dotnet-counters (dotnet-counters v8.0.1)
  - dotnet-gcdump (dotnet-gcdump v8.0.1)
  - dotnet-sos (dotnet-sos v8.0.1)

Warning: 1 tool(s) failed to install.
  - dotnet-stack: Package 'dotnet-stack' not found in configured sources.
```

#### Attempting to install metapackage locally:
```
$ dotnet tool install --local dotnet-diagnostics
Metapackages are only supported for global installation. Use 'dotnet tool install -g' or install individual tools locally.
```

## Creating Metapackages

### MSBuild Target Support

Add support in `Microsoft.NET.PackTool.targets` for creating metapackages:

```xml
<PropertyGroup>
  <!-- Set to true for metapackages -->
  <IsToolMetapackage>false</IsToolMetapackage>
</PropertyGroup>

<ItemGroup>
  <!-- Define LKG tools to include in metapackage with exact versions -->
  <MetapackageTool Include="dotnet-dump" Version="8.0.553" />
  <MetapackageTool Include="dotnet-trace" Version="8.0.553" />
  <MetapackageTool Include="dotnet-counters" Version="8.0.553" />
  <!-- etc -->
</ItemGroup>
```

The build task `GenerateToolsSettingsFile.cs` will be updated to generate Version 3 schema when `IsToolMetapackage` is true.

### Manual Creation

For custom scenarios, developers can create a metapackage manually:

1. Create a minimal project with no executable code
2. Create `DotnetToolSettings.xml` with Version="3" and metapackage tool list
3. Set `PackAsTool=true` and `IsToolMetapackage=true`
4. Run `dotnet pack`

## Testing Strategy

### Unit Tests

**New test file:** `test/Microsoft.DotNet.PackageInstall.Tests/MetapackageTests.cs`
- Test metapackage XML deserialization
- Test validation (RID-specific check, local install check)
- Test version range parsing

**New test file:** `test/dotnet.Tests/CommandTests/Tool/Install/ToolInstallMetapackageTests.cs`
- Test installing metapackage with multiple tools
- Test partial failure scenarios
- Test output message formatting
- Test PATH help shown only once
- Test rejection of local metapackage installation

### Integration Tests

**Update:** `test/Microsoft.NET.ToolPack.Tests/GivenThatWeWantToPackAToolProject.cs`
- Test packing a metapackage project
- Verify generated DotnetToolSettings.xml

**New test file:** `test/dotnet.Tests/ToolTests/EndToEndMetapackageTests.cs`
- End-to-end test: Create metapackage, install it, verify all tools work
- Test uninstalling metapackage (should uninstall all contained tools)

## Alternative Designs Considered

### Alternative 1: Use NuGet Dependencies Only
Instead of a custom XML format, rely purely on NuGet package dependencies.

**Rejected because:**
- No way to distinguish metapackages from regular packages with dependencies
- No control over version resolution strategy
- Can't provide good UX (consolidated output, PATH help shown once)
- Breaks existing tool installation semantics

### Alternative 2: Support RID-Specific Metapackages
Allow metapackages to specify different tools for different platforms.

**Rejected because:**
- Adds significant complexity
- Individual tools can already be RID-specific
- No clear use case that requires platform-specific metapackages
- Users can create platform-specific metapackages manually if needed

### Alternative 3: Support Local Metapackages
Allow metapackages in local tool manifests.

**Rejected because:**
- Conflicts with reproducible build goals (exact versions needed in manifest)
- Metapackages add indirection that complicates manifest management
- Primary use case doesn't require local installation
- Can be added later if real need emerges

### Alternative 4: Use Version Ranges Instead of Exact Versions
Allow metapackages to specify version ranges (e.g., "8.0.*") instead of exact versions.

**Rejected because:**
- Less safe - unpredictable which versions get installed
- Tools can race ahead with lower quality/testing, breaking the metapackage experience
- Tools with shared formats might become incompatible
- Harder to ensure uniform experience across all included tools
- LKG approach is more conservative and appropriate for the initial design
- Can be added as future enhancement if user demand emerges

## Backward Compatibility

- Existing tools (Version 1 and 2) continue to work unchanged
- Version 3 is only for metapackages
- Non-metapackage tools can use Version 2 indefinitely
- Old SDK versions will fail gracefully on Version 3 packages (unknown version error)

## Future Enhancements

### Potential additions for future versions:

1. **Metapackage updates**: `dotnet tool update -g <metapackage>` updates all contained tools to new LKG versions
2. **Metapackage uninstall**: `dotnet tool uninstall -g <metapackage>` uninstalls all contained tools
3. **Selective installation**: `dotnet tool install -g dotnet-diagnostics --include dotnet-dump,dotnet-trace`
4. **Tool groups in manifest**: Allow grouping related tools in local manifests
5. **Metapackage search**: Enhanced `dotnet tool search` to show metapackage contents
6. **Version ranges**: Support `VersionRange` attribute based on user demand, allowing flexibility when LKG precision isn't needed

## Open Questions

None. All design decisions have been made per the problem statement requirements.

## Summary

This design enables installing multiple related .NET tools with a single command, reducing cognitive load and simplifying common scenarios like container setup. The implementation:

- Extends the DotnetToolSettings.xml schema to Version 3 with metapackage support
- Uses Last Known Good (LKG) design with exact versions for safety and predictability
- Allows individual tools to evolve independently while metapackages provide stable combinations
- Ensures tools with shared formats are compatible through explicit version selection
- Prohibits combining metapackages with RID-specific packaging
- Restricts metapackages to global installation only
- Shows PATH configuration help only once per installation session
- Provides clear, consolidated output for metapackage installations
- Maintains backward compatibility with existing tools

The feature directly addresses the use cases outlined in issue #52609: AI tools, diagnostic tools, and SDK tool distribution.
