// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;

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

        internal static LaunchSettingsProfile? ReadLaunchProfile(string projectPath, string? launchProfileName, IReporter reporter)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            Debug.Assert(projectDirectory != null);

            var launchSettingsPath = CommonRunHelpers.GetPropertiesLaunchSettingsPath(projectDirectory, "Properties");
            bool hasLaunchSettings = File.Exists(launchSettingsPath);

            var projectNameWithoutExtension = Path.GetFileNameWithoutExtension(projectPath);
            var runJsonPath = CommonRunHelpers.GetFlatLaunchSettingsPath(projectDirectory, projectNameWithoutExtension);
            bool hasRunJson = File.Exists(runJsonPath);

            if (hasLaunchSettings)
            {
                if (hasRunJson)
                {
                    reporter.Warn(string.Format(CliCommandStrings.RunCommandWarningRunJsonNotUsed, runJsonPath, launchSettingsPath));
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
            catch (Exception ex)
            {
                reporter.Verbose($"Error reading '{launchSettingsPath}': {ex}.");
                return null;
            }

            if (string.IsNullOrEmpty(launchProfileName))
            {
                // Load the default (first) launch profile
                return ReadDefaultLaunchProfile(launchSettings, reporter);
            }

            // Load the specified launch profile
            var namedProfile = launchSettings?.Profiles?.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, launchProfileName, StringComparison.Ordinal)).Value;

            if (namedProfile is null)
            {
                reporter.Warn($"Unable to find launch profile with name '{launchProfileName}'. Falling back to default profile.");

                // Check if a case-insensitive match exists
                var caseInsensitiveNamedProfile = launchSettings?.Profiles?.FirstOrDefault(kvp =>
                    string.Equals(kvp.Key, launchProfileName, StringComparison.OrdinalIgnoreCase)).Key;

                if (caseInsensitiveNamedProfile is not null)
                {
                    reporter.Warn($"Note: Launch profile names are case-sensitive. Did you mean '{caseInsensitiveNamedProfile}'?");
                }

                return ReadDefaultLaunchProfile(launchSettings, reporter);
            }

            reporter.Verbose($"Found named launch profile '{launchProfileName}'.");
            namedProfile.LaunchProfileName = launchProfileName;
            return namedProfile;
        }

        private static LaunchSettingsProfile? ReadDefaultLaunchProfile(LaunchSettingsJson? launchSettings, IReporter reporter)
        {
            if (launchSettings is null || launchSettings.Profiles is null)
            {
                reporter.Verbose("Unable to find default launch profile.");
                return null;
            }

            var defaultProfileKey = launchSettings.Profiles.FirstOrDefault(entry => entry.Value.CommandName == "Project").Key;

            if (defaultProfileKey is null)
            {
                reporter.Verbose("Unable to find 'Project' command in the default launch profile.");
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
