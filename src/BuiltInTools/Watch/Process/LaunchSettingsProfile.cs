// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
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

        internal static LaunchSettingsProfile? ReadLaunchProfile(string projectPath, string? launchProfileName, ILogger logger)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            Debug.Assert(projectDirectory != null);

            var launchSettingsPath = GetPropertiesLaunchSettingsPath(projectDirectory, "Properties");
            bool hasLaunchSettings = File.Exists(launchSettingsPath);

            var projectNameWithoutExtension = Path.GetFileNameWithoutExtension(projectPath);
            var runJsonPath = GetFlatLaunchSettingsPath(projectDirectory, projectNameWithoutExtension);
            bool hasRunJson = File.Exists(runJsonPath);

            if (hasLaunchSettings)
            {
                if (hasRunJson)
                {
                    logger.LogWarning("Warning: Settings from '{JsonPath}' are not used because '{LaunchSettingsPath}' has precedence.", runJsonPath, launchSettingsPath);
                }
            }
            else if (hasRunJson)
            {
                launchSettingsPath = runJsonPath;
            }
            else
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
                return ReadDefaultLaunchProfile(launchSettings, logger);
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

                return ReadDefaultLaunchProfile(launchSettings, logger);
            }

            logger.LogDebug("Found named launch profile '{ProfileName}'.", launchProfileName);
            namedProfile.LaunchProfileName = launchProfileName;
            return namedProfile;
        }

        private static string GetPropertiesLaunchSettingsPath(string directoryPath, string propertiesDirectoryName)
            => Path.Combine(directoryPath, propertiesDirectoryName, "launchSettings.json");

        private static string GetFlatLaunchSettingsPath(string directoryPath, string projectNameWithoutExtension)
            => Path.Join(directoryPath, $"{projectNameWithoutExtension}.run.json");

        private static LaunchSettingsProfile? ReadDefaultLaunchProfile(LaunchSettingsJson? launchSettings, ILogger logger)
        {
            if (launchSettings is null || launchSettings.Profiles is null)
            {
                logger.LogDebug("Unable to find default launch profile.");
                return null;
            }

            var defaultProfileKey = launchSettings.Profiles.FirstOrDefault(entry => entry.Value.CommandName == "Project").Key;

            if (defaultProfileKey is null)
            {
                logger.LogDebug("Unable to find 'Project' command in the default launch profile.");
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
}
