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

#### 1.1 Core Configuration Builder

Create a centralized configuration builder that consolidates all configuration sources:

```csharp
public class DotNetConfiguration
{
    public static IConfiguration Create(string workingDirectory = null)
    {
        var builder = new ConfigurationBuilder();
        
        // Priority order (last wins):
        // 1. dotnet.config (if it exists)
        // 2. global.json (custom provider)
        // 3. Environment variables with DOTNET_ prefix
        // 4. Command line arguments (handled separately)
        
        workingDirectory ??= Directory.GetCurrentDirectory();
        
        // Add dotnet.config if it exists (future enhancement)
        var dotnetConfigPath = Path.Combine(workingDirectory, "dotnet.config");
        if (File.Exists(dotnetConfigPath))
        {
            builder.AddIniFile(dotnetConfigPath, optional: true, reloadOnChange: false);
        }
        
        // Add global.json with a custom configuration provider
        builder.Add(new GlobalJsonConfigurationSource(workingDirectory));
        
        // Add only DOTNET_ prefixed environment variables
        builder.AddEnvironmentVariables("DOTNET_");
        
        return builder.Build();
    }
}
```

#### 1.2 Global.json Configuration Provider

Create a custom configuration provider for global.json files:

```csharp
public class GlobalJsonConfigurationProvider : ConfigurationProvider
{
    private readonly string _path;
    
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
            var key = string.IsNullOrEmpty(prefix) 
                ? property.Name 
                : $"{prefix}:{property.Name}";
                
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    LoadGlobalJsonData(property.Value, key);
                    break;
                case JsonValueKind.String:
                    Data[key] = property.Value.GetString();
                    break;
                case JsonValueKind.Number:
                    Data[key] = property.Value.GetRawText();
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    Data[key] = property.Value.GetBoolean().ToString();
                    break;
            }
        }
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

public class GlobalJsonConfigurationSource : IConfigurationSource
{
    private readonly string _workingDirectory;
    
    public GlobalJsonConfigurationSource(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }
    
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new GlobalJsonConfigurationProvider(_workingDirectory);
    }
}
```

#### 1.3 Configuration Service Abstraction

Create a service interface for configuration access:

```csharp
public interface IConfigurationService
{
    IConfiguration Configuration { get; }
    string GetValue(string key, string defaultValue = null);
    T GetValue<T>(string key, T defaultValue = default);
    bool GetBoolValue(string key, bool defaultValue = false);
}

public class ConfigurationService : IConfigurationService
{
    public IConfiguration Configuration { get; }
    
    public ConfigurationService(IConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    
    public string GetValue(string key, string defaultValue = null)
    {
        return Configuration[key] ?? defaultValue;
    }
    
    public T GetValue<T>(string key, T defaultValue = default)
    {
        return Configuration.GetValue<T>(key, defaultValue);
    }
    
    public bool GetBoolValue(string key, bool defaultValue = false)
    {
        var value = Configuration[key];
        if (string.IsNullOrEmpty(value))
            return defaultValue;
            
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }
}
```

### Phase 2: Integration

#### 2.1 Update Program.cs

Update the main entry point to initialize configuration early:

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
        
        // Replace direct env var calls with configuration
        bool perfLogEnabled = ConfigurationService.GetBoolValue("DOTNET_CLI_PERF_LOG", false);
        
        // Continue with existing logic...
    }
}
```

#### 2.2 Configuration-Based Environment Provider

Create a bridge between the new configuration system and existing `IEnvironmentProvider` interface:

```csharp
public class ConfigurationBasedEnvironmentProvider : IEnvironmentProvider
{
    private readonly IConfigurationService _configurationService;
    private readonly IEnvironmentProvider _fallbackProvider;
    
    public ConfigurationBasedEnvironmentProvider(
        IConfigurationService configurationService, 
        IEnvironmentProvider fallbackProvider = null)
    {
        _configurationService = configurationService;
        _fallbackProvider = fallbackProvider ?? new EnvironmentProvider();
    }
    
    public string GetEnvironmentVariable(string name)
    {
        // For DOTNET_ prefixed variables, try configuration service first
        if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase))
        {
            var configValue = _configurationService.GetValue(name);
            if (configValue != null)
                return configValue;
        }
        
        // For all other variables or if not found in config, use fallback provider
        return _fallbackProvider.GetEnvironmentVariable(name);
    }
    
    public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
    {
        // For DOTNET_ prefixed variables, use configuration service
        if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase))
        {
            return _configurationService.GetBoolValue(name, defaultValue);
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

Replace direct environment variable access patterns:

**Before:**
```csharp
var value = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT");
bool optOut = !string.IsNullOrEmpty(value) && 
              (value.Equals("true", StringComparison.OrdinalIgnoreCase) || 
               value.Equals("1"));
```

**After:**
```csharp
bool optOut = ConfigurationService.GetBoolValue("DOTNET_CLI_TELEMETRY_OPTOUT", false);
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

**Note:** We will only add the `Microsoft.Extensions.Configuration.EnvironmentVariables` package but configure it to only process DOTNET_ prefixed variables. The `Microsoft.Extensions.Configuration.Ini` package is needed for future dotnet.config support.

## Configuration Key Mapping

### DOTNET_ Prefixed Environment Variables

Only DOTNET_ prefixed environment variables will be included in the unified configuration system:
- `DOTNET_CLI_TELEMETRY_OPTOUT` → `DOTNET_CLI_TELEMETRY_OPTOUT`
- `DOTNET_NOLOGO` → `DOTNET_NOLOGO`
- `DOTNET_CLI_PERF_LOG` → `DOTNET_CLI_PERF_LOG`

### System Environment Variables

System-level environment variables without the DOTNET_ prefix (e.g., `PATH`, `HOME`, `TEMP`, `USER`) will continue to be accessed directly through the existing `IEnvironmentProvider` interface and will not be part of the unified configuration system.

### Global.json Structure

Global.json properties will be flattened using colon notation:

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

Maps to:
- `sdk:version` → `"6.0.100"`
- `msbuild-sdks:Microsoft.Build.Traversal` → `"1.0.0"`

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

## Implementation Timeline

1. **Phase 1** (Infrastructure): 1-2 weeks
2. **Phase 2** (Integration): 1 week  
3. **Phase 3** (Migration): 2-3 weeks (incremental, can be spread across multiple PRs)
4. **Phase 4** (Testing): 1 week

Total estimated effort: 5-7 weeks

## Success Criteria

- [ ] All direct `Environment.GetEnvironmentVariable()` calls for DOTNET_ prefixed variables replaced
- [ ] All direct global.json reading replaced with configuration providers
- [ ] System environment variables continue to work through existing providers
- [ ] Comprehensive test coverage for new configuration system
- [ ] All existing functionality preserved (backward compatibility)
- [ ] Performance impact is negligible
- [ ] Documentation updated to reflect new configuration system
