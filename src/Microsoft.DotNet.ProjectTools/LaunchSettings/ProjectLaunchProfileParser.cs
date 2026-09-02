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
        Func<string, string>? getVariableValue,
        bool expandCommandLineArgs)
    {
        var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonSerializerContext.Default.ProjectLaunchProfile);
        if (profile == null)
        {
            return LaunchProfileParseResult.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        return LaunchProfileParseResult.Success(new ProjectLaunchProfile
        {
            LaunchProfileName = launchProfileName,
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs, getVariableValue, expandCommandLineArgs),
            LaunchBrowser = profile.LaunchBrowser,
            LaunchUrl = profile.LaunchUrl is null ? null : ExpandVariables(profile.LaunchUrl, getVariableValue: null),
            ApplicationUrl = profile.ApplicationUrl is null ? null : ExpandVariables(profile.ApplicationUrl, getVariableValue),
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables, getVariableValue),
        });
    }

    internal static bool RequiresMSBuildExpansion(ProjectLaunchProfile profile, bool includeCommandLineArgs)
        => (includeCommandLineArgs && LaunchProfileParser.RequiresMSBuildExpansion(profile.CommandLineArgs))
            || LaunchProfileParser.RequiresMSBuildExpansion(profile.ApplicationUrl)
            || profile.EnvironmentVariables.Values.Any(LaunchProfileParser.RequiresMSBuildExpansion);

    internal static ProjectLaunchProfile ExpandMSBuildProperties(
        ProjectLaunchProfile profile,
        Func<string, string> getVariableValue,
        bool expandCommandLineArgs,
        bool expandApplicationUrl)
        => new()
        {
            LaunchProfileName = profile.LaunchProfileName,
            CommandLineArgs = expandCommandLineArgs && profile.CommandLineArgs is not null
                ? ExpandMSBuildProperties(profile.CommandLineArgs, getVariableValue)
                : profile.CommandLineArgs,
            LaunchBrowser = profile.LaunchBrowser,
            LaunchUrl = profile.LaunchUrl,
            ApplicationUrl = !expandApplicationUrl || profile.ApplicationUrl is null
                ? profile.ApplicationUrl
                : ExpandMSBuildProperties(profile.ApplicationUrl, getVariableValue),
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ExpandMSBuildProperties(profile.EnvironmentVariables, getVariableValue),
        };
}
