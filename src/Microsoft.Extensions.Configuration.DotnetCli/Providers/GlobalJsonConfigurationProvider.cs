// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.Json;

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration provider that reads global.json files and maps keys to the canonical format.
/// </summary>
public class GlobalJsonConfigurationProvider : JsonConfigurationProvider
{
    private static readonly Dictionary<string, string> GlobalJsonKeyMappings = new()
    {
        ["sdk:version"] = "sdk:version",
        ["sdk:rollForward"] = "sdk:rollforward",
        ["sdk:allowPrerelease"] = "sdk:allowprerelease",
        ["msbuild-sdks"] = "msbuild:sdks",
        // Add more mappings as the global.json schema evolves
    };

    public GlobalJsonConfigurationProvider(GlobalJsonConfigurationSource source) : base(source)
    {
    }

    public override void Load()
    {
        base.Load();
        // Transform keys according to our mapping
        var transformedData = new Dictionary<string, string?>(Data.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in Data)
        {
            string key = kvp.Key;
            string? value = kvp.Value;
            var mappedKey = MapGlobalJsonKey(key);
            transformedData[mappedKey] = value;
        }

        // Replace the data with transformed keys
        Data = transformedData;
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
}
