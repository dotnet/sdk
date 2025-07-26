// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.Ini;

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration provider for dotnet.config INI files with key mapping.
/// Maps dotnet.config keys to the expected configuration structure.
/// </summary>
public class DotNetConfigurationProvider : IniConfigurationProvider
{
    private static readonly Dictionary<string, string> KeyMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Map INI section:key to expected configuration path
        ["dotnet.test.runner:name"] = "Test:RunnerName",
        
        // Future mappings can be added here for other dotnet.config settings
        // ["dotnet.example.section:key"] = "ConfigSection:Property",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetConfigurationProvider"/> class.
    /// </summary>
    /// <param name="source">The configuration source.</param>
    public DotNetConfigurationProvider(DotNetConfigurationSource source) : base(source)
    {
    }

    /// <summary>
    /// Loads configuration data with key transformation.
    /// </summary>
    public override void Load()
    {
        // Load the INI file normally first
        base.Load();

        // Transform keys according to our mapping
        var transformedData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in Data)
        {
            string key = kvp.Key;
            string? value = kvp.Value;

            // Check if we have a mapping for this key
            if (KeyMappings.TryGetValue(key, out string? mappedKey))
            {
                transformedData[mappedKey] = value;
            }
            else
            {
                // Keep unmapped keys as-is
                transformedData[key] = value;
            }
        }

        // Replace the data with transformed keys
        Data = transformedData;
    }
}
