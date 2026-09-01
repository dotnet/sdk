// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ProjectTools;

internal sealed class ProjectLaunchProfileParser : LaunchProfileParser
{
    public const string CommandName = "Project";

    public static readonly ProjectLaunchProfileParser Instance = new();

    private ProjectLaunchProfileParser()
    {
    }

    public override LaunchProfileParseResult ParseProfile(
        string launchSettingsPath,
        string? launchProfileName,
        string json,
        Func<string, string>? expandMSBuildProperty = null)
    {
        var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonSerializerContext.Default.ProjectLaunchProfile);
        if (profile == null)
        {
            return LaunchProfileParseResult.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        return LaunchProfileParseResult.Success(new ProjectLaunchProfile
        {
            LaunchProfileName = launchProfileName,
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs, expandMSBuildProperty),
            LaunchBrowser = profile.LaunchBrowser,
            LaunchUrl = profile.LaunchUrl is null ? null : ExpandVariables(profile.LaunchUrl, expandMSBuildProperty),
            ApplicationUrl = profile.ApplicationUrl is null ? null : ExpandVariables(profile.ApplicationUrl, expandMSBuildProperty),
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables, expandMSBuildProperty),
        });
    }
}
