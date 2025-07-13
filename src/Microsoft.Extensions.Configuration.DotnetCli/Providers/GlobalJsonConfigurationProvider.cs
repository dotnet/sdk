// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration provider that reads global.json files and maps keys to the canonical format.
/// </summary>
public class GlobalJsonConfigurationProvider : ConfigurationProvider
{
    private readonly string? _path;

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
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private string? FindGlobalJson(string startDirectory)
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
