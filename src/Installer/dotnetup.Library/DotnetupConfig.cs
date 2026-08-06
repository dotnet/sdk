// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// How dotnetup exposes the managed <c>dotnet</c> in the environment. This is the
/// dotnet-access axis; whether <c>dotnetup</c> itself is on PATH is a separate,
/// orthogonal setting (<see cref="DotnetupConfigData.DotnetupOnPath"/>).
/// </summary>
internal enum DotnetAccessMode
{
    /// <summary>No dotnet PATH wiring. User runs commands via <c>dotnetup dotnet</c>.</summary>
    None = 1,

    /// <summary>Add dotnetup-managed dotnet to a shell profile file.</summary>
    Shell = 2,

    /// <summary>Shell profile plus user-level env-var PATH/DOTNET_ROOT (so cmd.exe and GUI apps see the user dotnet too).</summary>
    Everywhere = 3,
}

/// <summary>
/// Persisted user configuration for dotnetup, stored alongside the manifest.
/// Records decisions made during the interactive init flow and via <c>dotnetup env</c>.
/// </summary>
internal class DotnetupConfigData
{
    public string SchemaVersion { get; set; } = "1";

    /// <summary>
    /// How the managed dotnet is exposed. Serialized as <c>accessMode</c> via the
    /// <see cref="DotnetAccessModeJsonConverter"/> (lowercase <c>none</c> / <c>shell</c> /
    /// <c>everywhere</c>).
    /// </summary>
    [JsonPropertyName("accessMode")]
    [JsonConverter(typeof(DotnetAccessModeJsonConverter))]
    public DotnetAccessMode AccessMode { get; set; } = DotnetAccessMode.Shell;

    /// <summary>
    /// Whether the dotnetup directory is on PATH so <c>dotnetup</c> can be invoked. Orthogonal
    /// to <see cref="AccessMode"/>. Defaults to <c>true</c> (and when absent from an older config).
    /// </summary>
    public bool DotnetupOnPath { get; set; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DotnetupConfigData))]
internal partial class DotnetupConfigJsonContext : JsonSerializerContext { }

/// <summary>
/// Reads and writes the dotnetup configuration file.
/// </summary>
internal static class DotnetupConfig
{
    /// <summary>
    /// Reads the config file if it exists, otherwise returns null.
    /// Uses GlobalJsonFileHelper for encoding-aware reading (handles BOM variants).
    /// A config written by an earlier internal build (legacy <c>pathPreference</c> property or
    /// pre-rename enum spellings) no longer maps: the unknown property is ignored and an
    /// unrecognized <c>accessMode</c> value is treated as corrupt, so the config re-defaults on the
    /// next write.
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
            string text;
            using (var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(path))
            using (var streamReader = new StreamReader(stream))
            {
                text = streamReader.ReadToEnd();
            }

            return JsonSerializer.Deserialize(text, DotnetupConfigJsonContext.Default.DotnetupConfigData);
        }
        catch (Exception ex)
        {
            Metrics.Tag(TelemetryTagNames.ConfigCorrupted, "true");
            Metrics.Tag(TelemetryTagNames.ConfigCorruptedError, ex.GetType().Name);
            SpectreAnsiConsole.MarkupLine(
                $"[{DotnetupTheme.Current.Warning}]Warning:[/] The dotnetup config file at {path.EscapeMarkup()} appears to be corrupted and could not be read: {ex.Message.EscapeMarkup()}");
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
    /// Returns the user's dotnet-access <see cref="DotnetAccessMode"/> from the config file if
    /// it exists, otherwise returns <c>null</c>.
    /// </summary>
    public static DotnetAccessMode? ReadAccessMode()
    {
        var config = Read();
        return config?.AccessMode;
    }

    /// <summary>
    /// Returns true if a config file exists, indicating the init flow has been completed.
    /// </summary>
    public static bool Exists() => File.Exists(DotnetupPaths.ConfigPath);
}
