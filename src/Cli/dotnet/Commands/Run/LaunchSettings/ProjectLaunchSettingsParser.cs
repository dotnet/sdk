// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal sealed class ProjectLaunchSettingsParser : LaunchProfileParser
{
    public const string CommandName = "Project";

    public static readonly ProjectLaunchSettingsParser Instance = new();

    private ProjectLaunchSettingsParser()
    {
    }

    public override LaunchProfileSettings ParseProfile(string launchSettingsPath, string? launchProfileName, string json)
    {
        var profile = JsonSerializer.Deserialize<ProjectLaunchProfileJson>(json);
        if (profile == null)
        {
            return LaunchProfileSettings.Failure(CliCommandStrings.LaunchProfileIsNotAJsonObject);
        }

        return LaunchProfileSettings.Success(new ProjectLaunchSettingsModel
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
