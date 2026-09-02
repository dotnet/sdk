// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.ProjectTools;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class LaunchSettingsProfile
{
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [JsonIgnore]
    public string? LaunchProfileName { get; set; }
    public string? ApplicationUrl { get; init; }
    public string? CommandName { get; init; }
    public bool LaunchBrowser { get; init; }
    public string? LaunchUrl { get; init; }

    internal static LaunchSettingsProfile? ReadLaunchProfile(
        ProjectRepresentation project,
        string? launchProfileName,
        ILogger logger,
        Func<string, string> expandMSBuildProperty)
    {
        var launchSettingsPath = LaunchSettings.TryFindLaunchSettingsFile(project.ProjectOrEntryPointFilePath, launchProfileName, (message, isError) =>
        {
            if (isError)
            {
                logger.LogError(message);
            }
            else
            {
                logger.LogWarning(message);
            }
        });

        if (launchSettingsPath == null)
        {
            return null;
        }

        LaunchSettingsJson? launchSettings;
        try
        {
            launchSettings = JsonSerializer.Deserialize<LaunchSettingsJson>(
                File.ReadAllText(launchSettingsPath),
                s_serializerOptions);
        }
        catch (Exception e)
        {
            logger.LogDebug("Error reading '{Path}': {Message}.", launchSettingsPath, e.Message);
            return null;
        }

        if (string.IsNullOrEmpty(launchProfileName))
        {
            // Load the default (first) launch profile
            return ExpandMSBuildProperties(ReadDefaultLaunchProfile(launchSettings, logger), expandMSBuildProperty, logger);
        }

        // Load the specified launch profile
        var namedProfile = launchSettings?.Profiles?.FirstOrDefault(kvp =>
            string.Equals(kvp.Key, launchProfileName, StringComparison.Ordinal)).Value;

        if (namedProfile is null)
        {
            logger.LogWarning("Unable to find launch profile with name '{ProfileName}'. Falling back to default profile.", launchProfileName);

            // Check if a case-insensitive match exists
            var caseInsensitiveNamedProfile = launchSettings?.Profiles?.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, launchProfileName, StringComparison.OrdinalIgnoreCase)).Key;

            if (caseInsensitiveNamedProfile is not null)
            {
                logger.LogWarning("Note: Launch profile names are case-sensitive. Did you mean '{ProfileName}'?", caseInsensitiveNamedProfile);
            }

            return ExpandMSBuildProperties(ReadDefaultLaunchProfile(launchSettings, logger), expandMSBuildProperty, logger);
        }

        logger.LogDebug("Found named launch profile '{ProfileName}'.", launchProfileName);
        namedProfile.LaunchProfileName = launchProfileName;
        return ExpandMSBuildProperties(namedProfile, expandMSBuildProperty, logger);
    }

    private static LaunchSettingsProfile? ExpandMSBuildProperties(
        LaunchSettingsProfile? profile,
        Func<string, string> expandMSBuildProperty,
        ILogger logger)
    {
        if (profile is null)
        {
            return null;
        }

        try
        {
            return new LaunchSettingsProfile
            {
                LaunchProfileName = profile.LaunchProfileName,
                ApplicationUrl = profile.ApplicationUrl,
                CommandName = profile.CommandName,
                LaunchBrowser = profile.LaunchBrowser,
                LaunchUrl = profile.LaunchUrl is null
                    ? null
                    : LaunchProfileParser.ExpandVariables(profile.LaunchUrl, expandMSBuildProperty),
            };
        }
        catch (InvalidProjectFileException e)
        {
            logger.LogDebug("Error expanding launch profile: {Message}.", e.Message);
            return null;
        }
    }

    private static LaunchSettingsProfile? ReadDefaultLaunchProfile(LaunchSettingsJson? launchSettings, ILogger logger)
    {
        if (launchSettings is null || launchSettings.Profiles is null)
        {
            logger.LogDebug("Unable to find default launch profile.");
            return null;
        }

        // Look for the first profile with a supported command name
        // Note: These must match the command names supported by LaunchSettingsManager in src/Cli/dotnet/Commands/Run/LaunchSettings/
        var supportedCommandNames = new[] { "Project", "Executable" };
        var defaultProfileKey = launchSettings.Profiles.FirstOrDefault(entry =>
            entry.Value.CommandName != null && supportedCommandNames.Contains(entry.Value.CommandName, StringComparer.Ordinal)).Key;

        if (defaultProfileKey is null)
        {
            logger.LogDebug("Unable to find a supported command name in the default launch profile. Supported types: {SupportedTypes}",
                string.Join(", ", supportedCommandNames));
            return null;
        }

        var defaultProfile = launchSettings.Profiles[defaultProfileKey];
        defaultProfile.LaunchProfileName = defaultProfileKey;
        return defaultProfile;
    }

    internal class LaunchSettingsJson
    {
        public OrderedDictionary<string, LaunchSettingsProfile>? Profiles { get; set; }
    }
}
