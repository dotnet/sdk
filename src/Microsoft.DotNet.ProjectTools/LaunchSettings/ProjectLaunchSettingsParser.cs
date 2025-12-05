// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.ProjectTools;

internal sealed class ProjectLaunchSettingsParser : LaunchProfileParser
{
    internal sealed class Json
    {
        [JsonPropertyName("commandName")]
        public string? CommandName { get; set; }

        [JsonPropertyName("commandLineArgs")]
        public string? CommandLineArgs { get; set; }

        [JsonPropertyName("launchBrowser")]
        public bool LaunchBrowser { get; set; }

        [JsonPropertyName("launchUrl")]
        public string? LaunchUrl { get; set; }

        [JsonPropertyName("applicationUrl")]
        public string? ApplicationUrl { get; set; }

        [JsonPropertyName("dotnetRunMessages")]
        public bool DotNetRunMessages { get; set; }

        [JsonPropertyName("environmentVariables")]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
    }

    public const string CommandName = "Project";

    public static readonly ProjectLaunchSettingsParser Instance = new();

    private ProjectLaunchSettingsParser()
    {
    }

    public override LaunchProfileSettings ParseProfile(string launchSettingsPath, string? launchProfileName, string json)
    {
        var profile = JsonSerializer.Deserialize<Json>(json);
        if (profile == null)
        {
            return LaunchProfileSettings.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        return LaunchProfileSettings.Success(new ProjectLaunchSettings
        {
            LaunchProfileName = launchProfileName,
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs),
            LaunchBrowser = profile.LaunchBrowser,
            LaunchUrl = profile.LaunchUrl,
            ApplicationUrl = profile.ApplicationUrl,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables),
        });
    }
}
