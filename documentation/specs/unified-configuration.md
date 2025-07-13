# Unified Configuration System for .NET CLI

## Overview

This specification outlines the plan to refactor the .NET CLI codebase to replace direct usage of global.json and environment variables with a unified configuration system based on Microsoft.Extensions.Configuration.IConfiguration.

## Goals

- Replace direct `Environment.GetEnvironmentVariable()` calls with a unified configuration system
- Replace direct `global.json` file reading with configuration providers
- Establish a clear configuration hierarchy and precedence
- Maintain backward compatibility with existing environment variables and global.json usage
- Provide a foundation for future configuration enhancements (e.g., dotnet.config support)

## Current State Analysis

The codebase currently has:
1. Direct `Environment.GetEnvironmentVariable()` calls throughout various classes
2. An `EnvironmentProvider` abstraction that wraps environment variable access
3. Custom `global.json` reading in several places (e.g., `GlobalJsonWorkloadSetsFile.cs`, `RuntimeConfig.cs`)
4. Some use of Microsoft.Extensions.Configuration in test infrastructure but not in the main CLI

## Configuration Hierarchy

The new unified configuration system will follow this precedence order (highest to lowest priority):

1. **Command-line arguments** (handled separately by existing System.CommandLine infrastructure)
2. **Environment variables with DOTNET_ prefix** (e.g., `DOTNET_CLI_TELEMETRY_OPTOUT`)
3. **global.json** (custom configuration provider)
4. **dotnet.config** (future enhancement - INI configuration file)

**Note:** System-level environment variables without the DOTNET_ prefix (e.g., `PATH`, `HOME`, `TEMP`) will continue to be accessed directly through the existing `IEnvironmentProvider` interface as they are not specific to .NET CLI configuration.

## Implementation Plan

### Phase 1: Infrastructure

#### 1.1 Core Configuration Builder with Key Mapping

Create a centralized configuration builder that consolidates all configuration sources with key mapping:

```csharp
public class DotNetConfiguration
{
    public static IConfiguration Create(string workingDirectory = null)
    {
        var builder = new ConfigurationBuilder();

        // Priority order (last wins):
        // 1. dotnet.config (if it exists) - with section-based key mapping
        // 2. global.json (custom provider with key mapping)
        // 3. Environment variables with DOTNET_ prefix (with key mapping)
        // 4. Command line arguments (handled separately)

        workingDirectory ??= Directory.GetCurrentDirectory();

        // Add dotnet.config if it exists (future enhancement)
        var dotnetConfigPath = Path.Combine(workingDirectory, "dotnet.config");
        if (File.Exists(dotnetConfigPath))
        {
            builder.AddIniFile(dotnetConfigPath, optional: true, reloadOnChange: false);
        }

        // Add global.json with a custom configuration provider that maps keys
        builder.Add(new GlobalJsonConfigurationSource(workingDirectory));

        // Add DOTNET_ prefixed environment variables with key mapping
        builder.Add(new DotNetEnvironmentConfigurationSource());

        return builder.Build();
    }
}
```

#### 1.2 Enhanced Global.json Configuration Provider

Create a custom configuration provider for global.json files with key mapping:

```csharp
public class GlobalJsonConfigurationProvider : ConfigurationProvider
{
    private readonly string _path;

    private static readonly Dictionary<string, string> GlobalJsonKeyMappings = new()
    {
        ["sdk:version"] = "sdk:version",
        ["sdk:rollForward"] = "sdk:rollforward",
        ["sdk:allowPrerelease"] = "sdk:allowprerelease",
        ["msbuild-sdks"] = "msbuild:sdks",
        // Add more mappings as the global.json schema evolves
    };

    public GlobalJsonConfigurationProvider(string workingDirectory)
    {
        _path = FindGlobalJson(workingDirectory);
    }

    public override void Load()
    {
        Data.Clear();

        if (_path == null || !File.Exists(_path))
            return;

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonDocument.Parse(json);

            LoadGlobalJsonData(document.RootElement, "");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error parsing global.json at {_path}", ex);
        }
    }

    private void LoadGlobalJsonData(JsonElement element, string prefix)
    {
        foreach (var property in element.EnumerateObject())
        {
            var rawKey = string.IsNullOrEmpty(prefix)
                ? property.Name
                : $"{prefix}:{property.Name}";

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    LoadGlobalJsonData(property.Value, rawKey);
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    // Map to canonical key format
                    var canonicalKey = MapGlobalJsonKey(rawKey);
                    Data[canonicalKey] = GetValueAsString(property.Value);
                    break;
            }
        }
    }

    private string MapGlobalJsonKey(string rawKey)
    {
        // Check for exact mapping first
        if (GlobalJsonKeyMappings.TryGetValue(rawKey, out var mapped))
            return mapped;

        // For msbuild-sdks, convert to msbuild:sdks:packagename format
        if (rawKey.StartsWith("msbuild-sdks:"))
            return rawKey.Replace("msbuild-sdks:", "msbuild:sdks:");

        // Default: convert to lowercase and normalize separators
        return rawKey.ToLowerInvariant().Replace("-", ":");
    }

    private string GetValueAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private string FindGlobalJson(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            var globalJsonPath = Path.Combine(current.FullName, "global.json");
            if (File.Exists(globalJsonPath))
                return globalJsonPath;
            current = current.Parent;
        }
        return null;
    }
}
```

#### 1.3 Environment Variable Configuration Provider with Key Mapping

Create a custom environment variable provider that maps DOTNET_ variables to canonical keys:

```csharp
public class DotNetEnvironmentConfigurationProvider : ConfigurationProvider
{
    private static readonly Dictionary<string, string> EnvironmentKeyMappings = new()
    {
        ["DOTNET_HOST_PATH"] = "dotnet:host:path",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "dotnet:cli:telemetry:optout",
        ["DOTNET_NOLOGO"] = "dotnet:cli:nologo",
        ["DOTNET_CLI_PERF_LOG"] = "dotnet:cli:perf:log",
        ["DOTNET_MULTILEVEL_LOOKUP"] = "dotnet:host:multilevel:lookup",
        ["DOTNET_ROLL_FORWARD"] = "dotnet:runtime:rollforward",
        ["DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX"] = "dotnet:runtime:rollforward:onnocandidate",
        ["DOTNET_CLI_HOME"] = "dotnet:cli:home",
        ["DOTNET_CLI_CONTEXT_VERBOSE"] = "dotnet:cli:context:verbose",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "dotnet:cli:firsttime:skip",
        ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "dotnet:tools:addtopath",
        // Add more mappings as needed
    };

    public override void Load()
    {
        Data.Clear();

        foreach (var mapping in EnvironmentKeyMappings)
        {
            var value = Environment.GetEnvironmentVariable(mapping.Key);
            if (!string.IsNullOrEmpty(value))
            {
                Data[mapping.Value] = value;
            }
        }
    }
}

public class DotNetEnvironmentConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotNetEnvironmentConfigurationProvider();
    }
}
```

#### 1.4 Updated Configuration Service with Key Lookup Helper

Update the configuration service to provide helper methods for common lookups:

```csharp
public interface IConfigurationService
{
    IConfiguration Configuration { get; }

    // Generic value access using canonical keys
    string GetValue(string canonicalKey, string defaultValue = null);
    T GetValue<T>(string canonicalKey, T defaultValue = default);
    bool GetBoolValue(string canonicalKey, bool defaultValue = false);

    // Helper methods for common configuration values
    bool IsTelemetryOptOut();
    bool IsNoLogo();
    string GetHostPath();
    string GetSdkVersion();
}

public class ConfigurationService : IConfigurationService
{
    public IConfiguration Configuration { get; }

    public ConfigurationService(IConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string GetValue(string canonicalKey, string defaultValue = null)
    {
        return Configuration[canonicalKey] ?? defaultValue;
    }

    public T GetValue<T>(string canonicalKey, T defaultValue = default)
    {
        return Configuration.GetValue<T>(canonicalKey, defaultValue);
    }

    public bool GetBoolValue(string canonicalKey, bool defaultValue = false)
    {
        var value = Configuration[canonicalKey];
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    // Helper methods for common configuration values
    public bool IsTelemetryOptOut() => GetBoolValue("dotnet:cli:telemetry:optout", false);
    public bool IsNoLogo() => GetBoolValue("dotnet:cli:nologo", false);
    public string GetHostPath() => GetValue("dotnet:host:path");
    public string GetSdkVersion() => GetValue("sdk:version");
}
```

### Phase 2: Integration

#### 2.1 Update Program.cs

Update the main entry point to initialize configuration early with canonical key access:

```csharp
public class Program
{
    public static IConfiguration GlobalConfiguration { get; private set; }
    public static IConfigurationService ConfigurationService { get; private set; }

    public static int Main(string[] args)
    {
        // Initialize configuration early
        GlobalConfiguration = DotNetConfiguration.Create();
        ConfigurationService = new ConfigurationService(GlobalConfiguration);

        // Replace direct env var calls with configuration using canonical keys
        bool perfLogEnabled = ConfigurationService.GetBoolValue("dotnet:cli:perf:log", false);
        bool noLogo = ConfigurationService.IsNoLogo();

        // Continue with existing logic...
    }
}
```

#### 2.2 Configuration-Based Environment Provider with Key Mapping

Create a bridge between the new configuration system and existing `IEnvironmentProvider` interface:

```csharp
public class ConfigurationBasedEnvironmentProvider : IEnvironmentProvider
{
    private readonly IConfigurationService _configurationService;
    private readonly IEnvironmentProvider _fallbackProvider;

    // Reverse mapping from environment variable names to canonical keys
    private static readonly Dictionary<string, string> EnvironmentToCanonicalMappings = new()
    {
        ["DOTNET_HOST_PATH"] = "dotnet:host:path",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "dotnet:cli:telemetry:optout",
        ["DOTNET_NOLOGO"] = "dotnet:cli:nologo",
        ["DOTNET_CLI_PERF_LOG"] = "dotnet:cli:perf:log",
        ["DOTNET_MULTILEVEL_LOOKUP"] = "dotnet:host:multilevel:lookup",
        ["DOTNET_ROLL_FORWARD"] = "dotnet:runtime:rollforward",
        ["DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX"] = "dotnet:runtime:rollforward:onnocandidate",
        // Add more mappings as needed
    };

    public ConfigurationBasedEnvironmentProvider(
        IConfigurationService configurationService,
        IEnvironmentProvider fallbackProvider = null)
    {
        _configurationService = configurationService;
        _fallbackProvider = fallbackProvider ?? new EnvironmentProvider();
    }

    public string GetEnvironmentVariable(string name)
    {
        // For DOTNET_ prefixed variables, try configuration service first using canonical key
        if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) &&
            EnvironmentToCanonicalMappings.TryGetValue(name, out var canonicalKey))
        {
            var configValue = _configurationService.GetValue(canonicalKey);
            if (configValue != null)
                return configValue;
        }

        // For all other variables or if not found in config, use fallback provider
        return _fallbackProvider.GetEnvironmentVariable(name);
    }

    public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
    {
        // For DOTNET_ prefixed variables, use configuration service with canonical key
        if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) &&
            EnvironmentToCanonicalMappings.TryGetValue(name, out var canonicalKey))
        {
            return _configurationService.GetBoolValue(canonicalKey, defaultValue);
        }

        // For all other variables, use fallback provider
        return _fallbackProvider.GetEnvironmentVariableAsBool(name, defaultValue);
    }

    // Implement other members as pass-through to fallback provider
    public IEnumerable<string> ExecutableExtensions => _fallbackProvider.ExecutableExtensions;

    public string GetCommandPath(string commandName, params string[] extensions)
        => _fallbackProvider.GetCommandPath(commandName, extensions);

    public string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
        => _fallbackProvider.GetCommandPathFromRootPath(rootPath, commandName, extensions);

    public string GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions)
        => _fallbackProvider.GetCommandPathFromRootPath(rootPath, commandName, extensions);

    public string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
        => _fallbackProvider.GetEnvironmentVariable(variable, target);

    public void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
        => _fallbackProvider.SetEnvironmentVariable(variable, value, target);
}
```

### Phase 3: Migration

#### 3.1 Systematic Replacement

Replace direct environment variable access patterns using canonical keys:

**Before:**
```csharp
var value = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT");
bool optOut = !string.IsNullOrEmpty(value) &&
              (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1"));
```

**After (using canonical key):**
```csharp
bool optOut = ConfigurationService.GetBoolValue("dotnet:cli:telemetry:optout", false);
// Or using the helper method:
bool optOut = ConfigurationService.IsTelemetryOptOut();
```

**Before:**
```csharp
var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
```

**After (using canonical key):**
```csharp
var hostPath = ConfigurationService.GetValue("dotnet:host:path");
// Or using the helper method:
var hostPath = ConfigurationService.GetHostPath();
```

#### 3.2 Update Direct global.json Readers

Classes that currently read global.json directly should be updated to use the configuration system:

**Files to update:**
- `src/Cli/dotnet/Commands/Workload/GlobalJsonWorkloadSetFile.cs`
- `src/Cli/dotnet/RuntimeConfig.cs`
- Any other direct global.json readers found during implementation

#### 3.3 Update Environment Provider Usages

Update all instances where `IEnvironmentProvider` is used to use the new `ConfigurationBasedEnvironmentProvider`.

### Phase 4: Testing & Validation

#### 4.1 Backward Compatibility Testing

Ensure all existing functionality continues to work:
- All DOTNET_ prefixed environment variables continue to be respected
- System environment variables (PATH, HOME, etc.) continue to work through the fallback provider
- global.json files are read correctly
- Precedence rules work as expected

#### 4.2 Unit Tests

Create comprehensive unit tests for:
- `DotNetConfiguration` class
- `GlobalJsonConfigurationProvider`
- `ConfigurationService`
- `ConfigurationBasedEnvironmentProvider`

#### 4.3 Integration Tests

Add integration tests to verify:
- Configuration hierarchy works correctly for DOTNET_ prefixed variables
- System environment variables continue to work through the fallback provider
- global.json files in various directory structures are found
- Environment variable override behavior works correctly

## Package Dependencies

The following NuGet package references will need to be added to relevant projects:

- `Microsoft.Extensions.Configuration`
- `Microsoft.Extensions.Configuration.Abstractions`
- `Microsoft.Extensions.Configuration.EnvironmentVariables`
- `Microsoft.Extensions.Configuration.Json`
- `Microsoft.Extensions.Configuration.Ini`
- `Microsoft.Extensions.Configuration.Binder`

**Note:** We will create a custom environment variable provider that only processes DOTNET_ prefixed variables and maps them to canonical keys. The `Microsoft.Extensions.Configuration.Ini` package is needed for future dotnet.config support.

## Key Mapping Reference

### Environment Variables → Canonical Keys
- `DOTNET_CLI_TELEMETRY_OPTOUT` → `dotnet:cli:telemetry:optout`
- `DOTNET_NOLOGO` → `dotnet:cli:nologo`
- `DOTNET_HOST_PATH` → `dotnet:host:path`
- `DOTNET_CLI_PERF_LOG` → `dotnet:cli:perf:log`
- `DOTNET_MULTILEVEL_LOOKUP` → `dotnet:host:multilevel:lookup`
- `DOTNET_ROLL_FORWARD` → `dotnet:runtime:rollforward`

### Global.json → Canonical Keys
```json
{
  "sdk": {
    "version": "6.0.100"
  },
  "msbuild-sdks": {
    "Microsoft.Build.Traversal": "1.0.0"
  }
}
```

Maps to canonical keys:
- `sdk:version` → `"6.0.100"`
- `msbuild:sdks:Microsoft.Build.Traversal` → `"1.0.0"`

### INI Configuration → Canonical Keys
```ini
[dotnet.cli]
telemetryOptOut=true
nologo=true

[sdk]
version=6.0.100
```

Maps to canonical keys:
- `dotnet:cli:telemetry:optout` → `"true"`
- `dotnet:cli:nologo` → `"true"`
- `sdk:version` → `"6.0.100"`

**Note:** System-level environment variables without the DOTNET_ prefix (e.g., `PATH`, `HOME`, `TEMP`, `USER`) will continue to be accessed directly through the existing `IEnvironmentProvider` interface and will not be part of the unified configuration system.

## Typed Configuration Models

### Functional Configuration Groupings

Based on analysis of the existing codebase, the following functional groupings have been identified for typed configuration models:

#### 1. **CLI User Experience Configuration**
Settings that control the CLI's user interface and interaction behavior:

```csharp
public class CliUserExperienceConfiguration
{
    public bool TelemetryOptOut { get; set; } = false;
    public bool NoLogo { get; set; } = false;
    public bool ForceUtf8Encoding { get; set; } = false;
    public string? UILanguage { get; set; }
    public string? TelemetryProfile { get; set; }
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_TELEMETRY_OPTOUT` → `TelemetryOptOut`
- `DOTNET_NOLOGO` → `NoLogo`
- `DOTNET_CLI_FORCE_UTF8_ENCODING` → `ForceUtf8Encoding`
- `DOTNET_CLI_UI_LANGUAGE` → `UILanguage`
- `DOTNET_CLI_TELEMETRY_PROFILE` → `TelemetryProfile`

#### 2. **Runtime Host Configuration**
Settings that control .NET runtime host behavior:

```csharp
public class RuntimeHostConfiguration
{
    public string? HostPath { get; set; }
    public bool MultilevelLookup { get; set; } = true;
    public string? RollForward { get; set; }
    public string? RollForwardOnNoCandidateFx { get; set; }
    public string? Root { get; set; }
    public string? RootX86 { get; set; }
}
```

**Environment Variables Mapped:**
- `DOTNET_HOST_PATH` → `HostPath`
- `DOTNET_MULTILEVEL_LOOKUP` → `MultilevelLookup`
- `DOTNET_ROLL_FORWARD` → `RollForward`
- `DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX` → `RollForwardOnNoCandidateFx`
- `DOTNET_ROOT` → `Root`
- `DOTNET_ROOT(x86)` → `RootX86`

#### 3. **Build and MSBuild Configuration**
Settings that control build system behavior:

```csharp
public class BuildConfiguration
{
    public bool RunMSBuildOutOfProc { get; set; } = false;
    public bool UseMSBuildServer { get; set; } = false;
    public string? ConfigureMSBuildTerminalLogger { get; set; }
    public bool DisablePublishAndPackRelease { get; set; } = false;
    public bool LazyPublishAndPackReleaseForSolutions { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC` → `RunMSBuildOutOfProc`
- `DOTNET_CLI_USE_MSBUILD_SERVER` → `UseMSBuildServer`
- `DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER` → `ConfigureMSBuildTerminalLogger`
- `DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE` → `DisablePublishAndPackRelease`
- `DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS` → `LazyPublishAndPackReleaseForSolutions`

#### 4. **SDK Resolver Configuration**
Settings that control SDK resolution and discovery:

```csharp
public class SdkResolverConfiguration
{
    public bool EnableLog { get; set; } = false;
    public string? SdksDirectory { get; set; }
    public string? SdksVersion { get; set; }
    public string? CliDirectory { get; set; }
}
```

**Environment Variables Mapped:**
- `DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG` → `EnableLog`
- `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR` → `SdksDirectory`
- `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER` → `SdksVersion`
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` → `CliDirectory`

#### 5. **Workload Configuration**
Settings that control workload management and behavior:

```csharp
public class WorkloadConfiguration
{
    public bool UpdateNotifyDisable { get; set; } = false;
    public int UpdateNotifyIntervalHours { get; set; } = 24;
    public bool DisablePackGroups { get; set; } = false;
    public bool SkipIntegrityCheck { get; set; } = false;
    public string[]? ManifestRoots { get; set; }
    public string[]? PackRoots { get; set; }
    public bool ManifestIgnoreDefaultRoots { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE` → `UpdateNotifyDisable`
- `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS` → `UpdateNotifyIntervalHours`
- `DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS` → `DisablePackGroups`
- `DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK` → `SkipIntegrityCheck`
- `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS` → `ManifestRoots`
- `DOTNETSDK_WORKLOAD_PACK_ROOTS` → `PackRoots`
- `DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS` → `ManifestIgnoreDefaultRoots`

#### 6. **First Time Use Configuration**
Settings that control first-time user experience setup:

```csharp
public class FirstTimeUseConfiguration
{
    public bool GenerateAspNetCertificate { get; set; } = true;
    public bool AddGlobalToolsToPath { get; set; } = true;
    public bool SkipFirstTimeExperience { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_GENERATE_ASPNET_CERTIFICATE` → `GenerateAspNetCertificate`
- `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH` → `AddGlobalToolsToPath`
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` → `SkipFirstTimeExperience`

#### 7. **Development and Debugging Configuration**
Settings that control development tools and debugging features:

```csharp
public class DevelopmentConfiguration
{
    public bool PerfLogEnabled { get; set; } = false;
    public string? PerfLogCount { get; set; }
    public string? CliHome { get; set; }
    public bool ContextVerbose { get; set; } = false;
    public bool AllowTargetingPackCaching { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_PERF_LOG` → `PerfLogEnabled`
- `DOTNET_PERF_LOG_COUNT` → `PerfLogCount`
- `DOTNET_CLI_HOME` → `CliHome`
- `DOTNET_CLI_CONTEXT_VERBOSE` → `ContextVerbose`
- `DOTNETSDK_ALLOW_TARGETING_PACK_CACHING` → `AllowTargetingPackCaching`

#### 8. **Tool and Global Tool Configuration**
Settings that control global tools behavior:

```csharp
public class ToolConfiguration
{
    public bool AllowManifestInRoot { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT` → `AllowManifestInRoot`

### Typed Configuration Service Interface

```csharp
public interface ITypedConfigurationService
{
    IConfiguration RawConfiguration { get; }

    // Typed configuration access
    CliUserExperienceConfiguration CliUserExperience { get; }
    RuntimeHostConfiguration RuntimeHost { get; }
    BuildConfiguration Build { get; }
    SdkResolverConfiguration SdkResolver { get; }
    WorkloadConfiguration Workload { get; }
    FirstTimeUseConfiguration FirstTimeUse { get; }
    DevelopmentConfiguration Development { get; }
    ToolConfiguration Tool { get; }

    // Event for configuration changes (if we support hot reload)
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}

public class TypedConfigurationService : ITypedConfigurationService
{
    public IConfiguration RawConfiguration { get; }

    public CliUserExperienceConfiguration CliUserExperience { get; }
    public RuntimeHostConfiguration RuntimeHost { get; }
    public BuildConfiguration Build { get; }
    public SdkResolverConfiguration SdkResolver { get; }
    public WorkloadConfiguration Workload { get; }
    public FirstTimeUseConfiguration FirstTimeUse { get; }
    public DevelopmentConfiguration Development { get; }
    public ToolConfiguration Tool { get; }

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public TypedConfigurationService(IConfiguration configuration)
    {
        RawConfiguration = configuration;

        // Bind configuration sections to typed models
        CliUserExperience = new CliUserExperienceConfiguration();
        configuration.GetSection("dotnet:cli").Bind(CliUserExperience);

        RuntimeHost = new RuntimeHostConfiguration();
        configuration.GetSection("dotnet:host").Bind(RuntimeHost);

        Build = new BuildConfiguration();
        configuration.GetSection("dotnet:build").Bind(Build);

        SdkResolver = new SdkResolverConfiguration();
        configuration.GetSection("dotnet:sdkresolver").Bind(SdkResolver);

        Workload = new WorkloadConfiguration();
        configuration.GetSection("dotnet:workload").Bind(Workload);

        FirstTimeUse = new FirstTimeUseConfiguration();
        configuration.GetSection("dotnet:firsttime").Bind(FirstTimeUse);

        Development = new DevelopmentConfiguration();
        configuration.GetSection("dotnet:development").Bind(Development);

        Tool = new ToolConfiguration();
        configuration.GetSection("dotnet:tool").Bind(Tool);
    }
}
```

### Benefits of Typed Configuration Models

1. **Intellisense and Compile-time Safety**: Developers get full IDE support and compile-time checking
2. **Logical Grouping**: Related settings are grouped together functionally
3. **Type Safety**: Boolean values are actual booleans, integers are integers, etc.
4. **Default Values**: Clear default values are defined in the model classes
5. **Discoverability**: Developers can explore configuration options through the object model
6. **Validation**: Can add data annotations for validation
7. **Documentation**: Each property can have XML documentation

### Usage Examples

```csharp
// Instead of:
bool telemetryOptOut = configService.GetBoolValue("dotnet:cli:telemetry:optout", false);
bool noLogo = configService.GetBoolValue("dotnet:cli:nologo", false);

// Developers can write:
var cliConfig = typedConfigService.CliUserExperience;
bool telemetryOptOut = cliConfig.TelemetryOptOut;
bool noLogo = cliConfig.NoLogo;

// Or for the common case:
if (typedConfigService.CliUserExperience.TelemetryOptOut)
{
    // Skip telemetry
}
```

This approach eliminates the need to remember canonical key names and provides a much more developer-friendly API.

## Error Handling

- **Malformed global.json**: Log warning and continue without global.json configuration
- **Missing files**: Silently ignore missing optional configuration files
- **Configuration access errors**: Provide meaningful error messages and fallback to defaults

## Future Enhancements

### dotnet.config Support

A future enhancement could add support for a `dotnet.config` INI file that provides project-specific configuration:

```ini
[cli]
telemetryOptOut=true
noLogo=true

[build]
configuration=Release
```

This would map to configuration keys like:
- `cli:telemetryOptOut` → `"true"`
- `cli:noLogo` → `"true"`
- `build:configuration` → `"Release"`

### Configuration Schema Validation

Consider adding schema validation for configuration files to provide better error messages. For INI files, this would involve validating known sections and keys.

### Configuration Hot Reload

For development scenarios, consider adding support for configuration hot reload when files change.

## Breaking Changes

This refactoring should not introduce any breaking changes as it maintains backward compatibility with:
- All existing DOTNET_ prefixed environment variables
- All system environment variables (accessed through fallback provider)
- All existing global.json file structures and locations
- All existing APIs and interfaces (through adapter patterns)


## Success Criteria

- [ ] All direct `Environment.GetEnvironmentVariable()` calls for DOTNET_ prefixed variables replaced
- [ ] All direct global.json reading replaced with configuration providers
- [ ] System environment variables continue to work through existing providers
- [ ] **Key mapping system implemented and tested for all configuration sources**
- [ ] **Canonical key format consistently used throughout the codebase**
- [ ] **Helper methods available for common configuration lookups**
- [ ] Comprehensive test coverage for new configuration system including key mapping
- [ ] All existing functionality preserved (backward compatibility)
- [ ] Performance impact is negligible
- [ ] Documentation updated to reflect new configuration system and canonical key format
