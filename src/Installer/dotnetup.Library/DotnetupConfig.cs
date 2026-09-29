// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
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

    /// <summary>Shell profile plus system PATH and user-level DOTNET_ROOT (so cmd.exe and GUI apps see the user dotnet too).</summary>
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
    /// The persisted dotnet-access choice, serialized as <c>accessMode</c> via the
    /// <see cref="DotnetAccessModeJsonConverter"/> (lowercase <c>none</c> / <c>shell</c> /
    /// <c>everywhere</c>). <c>null</c> when the user has not completed setup; a config that only
    /// stores other settings (such as <see cref="UpdateNotifications"/>) must not look onboarded.
    /// </summary>
    [JsonPropertyName("accessMode")]
    [JsonConverter(typeof(DotnetAccessModeJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DotnetAccessMode? ConfiguredAccessMode { get; set; }

    /// <summary>
    /// How the managed dotnet is exposed. <see cref="DotnetupConfig.Read"/> only returns configs
    /// where this was recorded; the <see cref="DotnetAccessMode.Shell"/> fallback applies only to
    /// configs constructed in code without an access mode.
    /// </summary>
    [JsonIgnore]
    public DotnetAccessMode AccessMode
    {
        get => ConfiguredAccessMode ?? DotnetAccessMode.Shell;
        set => ConfiguredAccessMode = value;
    }

    /// <summary>
    /// Whether the dotnetup directory is on PATH so <c>dotnetup</c> can be invoked. Orthogonal
    /// to <see cref="AccessMode"/>. Defaults to <c>true</c> (and when absent from an older config).
    /// </summary>
    public bool DotnetupOnPath { get; set; } = true;

    /// <summary>
    /// Whether install and update commands may mention that a newer dotnetup is available.
    /// Defaults to <c>true</c> (and when absent from an older config). Set by
    /// <c>dotnetup self update --nowarn</c>.
    /// </summary>
    public bool UpdateNotifications { get; set; } = true;
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
    /// Reads the setup config if the file records an access mode, otherwise returns null.
    /// Uses GlobalJsonFileHelper for encoding-aware reading (handles BOM variants).
    /// A config written by an earlier internal build (legacy <c>pathPreference</c> property or
    /// pre-rename enum spellings) no longer maps: without an <c>accessMode</c> the config is treated
    /// as not yet set up, and an unrecognized <c>accessMode</c> value is treated as corrupt, so the
    /// config re-defaults on the next write.
    /// </summary>
    public static DotnetupConfigData? Read()
    {
        var state = TryLoad(out var config, out var error);
        if (state == ConfigFileState.Corrupt)
        {
            Metrics.Tag(TelemetryTagNames.ConfigCorrupted, "true");
            Metrics.Tag(TelemetryTagNames.ConfigCorruptedError, error!.GetType().Name);
            SpectreAnsiConsole.MarkupLine(
                $"[{DotnetupTheme.Current.Warning}]Warning:[/] The dotnetup config file at {DotnetupPaths.ConfigPath.EscapeMarkup()} appears to be corrupted and could not be read: {error.Message.EscapeMarkup()}");
            return null;
        }

        return config?.ConfiguredAccessMode is null ? null : config;
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
    /// Persists the environment setup choices while preserving unrelated settings.
    /// </summary>
    public static DotnetupConfigData WriteAccessSettings(DotnetAccessMode accessMode, bool dotnetupOnPath)
    {
        var config = new DotnetupConfigData
        {
            AccessMode = accessMode,
            DotnetupOnPath = dotnetupOnPath,
            UpdateNotifications = ReadUpdateNotificationsEnabled(),
        };
        Write(config);
        return config;
    }

    /// <summary>
    /// Returns whether update notifications are enabled. Silent: a missing or unreadable config
    /// keeps the default rather than warning, because this runs alongside commands that already
    /// report config problems.
    /// </summary>
    public static bool ReadUpdateNotificationsEnabled()
        => TryLoad(out var config, out _) != ConfigFileState.Loaded || config!.UpdateNotifications;

    /// <summary>
    /// Enables or disables update notifications without recording or changing the setup choices.
    /// </summary>
    public static void SetUpdateNotifications(bool enabled)
    {
        var state = TryLoad(out var config, out _);
        if (state == ConfigFileState.Corrupt)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UserConfigurationCorrupted,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateNotificationsConfigUnreadable, DotnetupPaths.ConfigPath));
        }

        config ??= new DotnetupConfigData();
        config.UpdateNotifications = enabled;
        Write(config);
    }

    /// <summary>
    /// Returns the user's dotnet-access <see cref="DotnetAccessMode"/> from the config file if
    /// setup recorded one, otherwise returns <c>null</c>.
    /// </summary>
    public static DotnetAccessMode? ReadAccessMode()
    {
        var config = Read();
        return config?.AccessMode;
    }

    /// <summary>
    /// Returns true if a config file records the init choices, or exists but cannot be read.
    /// A config holding only other settings does not indicate that init has been completed.
    /// </summary>
    public static bool Exists() => TryLoad(out var config, out _) switch
    {
        ConfigFileState.Corrupt => true,
        ConfigFileState.Loaded => config!.ConfiguredAccessMode is not null,
        _ => false,
    };

    private enum ConfigFileState
    {
        Missing,
        Corrupt,
        Loaded,
    }

    private static ConfigFileState TryLoad(out DotnetupConfigData? config, out Exception? error)
    {
        config = null;
        error = null;
        var path = DotnetupPaths.ConfigPath;
        if (!File.Exists(path))
        {
            return ConfigFileState.Missing;
        }

        try
        {
            string text;
            using (var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(path))
            using (var streamReader = new StreamReader(stream))
            {
                text = streamReader.ReadToEnd();
            }

            config = JsonSerializer.Deserialize(text, DotnetupConfigJsonContext.Default.DotnetupConfigData)
                ?? throw new JsonException("The config file is empty.");
            return ConfigFileState.Loaded;
        }
        catch (Exception ex)
        {
            config = null;
            error = ex;
            return ConfigFileState.Corrupt;
        }
    }
}
