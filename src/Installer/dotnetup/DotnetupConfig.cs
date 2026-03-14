// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Represents the user's path replacement preference chosen during the walkthrough.
/// </summary>
internal enum PathPreference
{
    /// <summary>No PATH replacement. User runs commands via <c>dotnetup dotnet</c>.</summary>
    DotnetupDotnet = 1,

    /// <summary>Add dotnetup-managed dotnet to a shell profile file.</summary>
    ShellProfile = 2,

    /// <summary>Full PATH and DOTNET_ROOT replacement (the existing set-default-install behavior).</summary>
    FullPathReplacement = 3,
}

/// <summary>
/// Persisted user configuration for dotnetup, stored alongside the manifest.
/// Records decisions made during the interactive walkthrough.
/// </summary>
internal class DotnetupConfigData
{
    public string SchemaVersion { get; set; } = "1";
    public PathPreference PathPreference { get; set; } = PathPreference.FullPathReplacement;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(DotnetupConfigData))]
[JsonSerializable(typeof(PathPreference))]
internal partial class DotnetupConfigJsonContext : JsonSerializerContext { }

/// <summary>
/// Reads and writes the dotnetup configuration file.
/// </summary>
internal static class DotnetupConfig
{
    /// <summary>
    /// Reads the config file if it exists, otherwise returns null.
    /// </summary>
    public static DotnetupConfigData? Read()
    {
        var path = DotnetupPaths.ConfigPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, DotnetupConfigJsonContext.Default.DotnetupConfigData);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the config file, creating the directory if necessary.
    /// </summary>
    public static void Write(DotnetupConfigData config)
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        var json = JsonSerializer.Serialize(config, DotnetupConfigJsonContext.Default.DotnetupConfigData);
        File.WriteAllText(DotnetupPaths.ConfigPath, json);
    }

    /// <summary>
    /// Returns true if a config file exists, indicating the walkthrough has been completed.
    /// </summary>
    public static bool Exists() => File.Exists(DotnetupPaths.ConfigPath);
}
