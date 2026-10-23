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
        Func<string, string>? evaluateExpression,
        bool expandCommandLineArgs)
    {
        var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonSerializerContext.Default.ProjectLaunchProfile);
        if (profile == null)
        {
            return LaunchProfileParseResult.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        if (!TryParseWorkingDirectory(launchSettingsPath, profile.WorkingDirectory, evaluateExpression, out var workingDirectory, out var error))
        {
            return LaunchProfileParseResult.Failure(error);
        }

        return LaunchProfileParseResult.Success(new ProjectLaunchProfile
        {
            LaunchProfileName = launchProfileName,
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs, evaluateExpression, expandCommandLineArgs),
            LaunchBrowser = profile.LaunchBrowser,
            LaunchUrl = profile.LaunchUrl is null ? null : ExpandVariables(profile.LaunchUrl, evaluateExpression: null),
            ApplicationUrl = profile.ApplicationUrl is null ? null : ExpandVariables(profile.ApplicationUrl, evaluateExpression),
            WorkingDirectory = workingDirectory,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables, evaluateExpression),
        });
    }

    internal static bool RequiresMSBuildExpansion(ProjectLaunchProfile profile, bool includeCommandLineArgs)
        => (includeCommandLineArgs && LaunchProfileParser.RequiresMSBuildExpansion(profile.CommandLineArgs))
            || LaunchProfileParser.RequiresMSBuildExpansion(profile.ApplicationUrl)
            || profile.EnvironmentVariables.Values.Any(LaunchProfileParser.RequiresMSBuildExpansion);

    internal static ProjectLaunchProfile ExpandMSBuildProperties(
        ProjectLaunchProfile profile,
        Func<string, string> evaluateExpression,
        bool expandCommandLineArgs,
        bool expandApplicationUrl)
        => new()
        {
            LaunchProfileName = profile.LaunchProfileName,
            CommandLineArgs = expandCommandLineArgs && profile.CommandLineArgs is not null
                ? ExpandMSBuildProperties(profile.CommandLineArgs, evaluateExpression)
                : profile.CommandLineArgs,
            LaunchBrowser = profile.LaunchBrowser,
            LaunchUrl = profile.LaunchUrl,
            ApplicationUrl = !expandApplicationUrl || profile.ApplicationUrl is null
                ? profile.ApplicationUrl
                : ExpandMSBuildProperties(profile.ApplicationUrl, evaluateExpression),
            WorkingDirectory = profile.WorkingDirectory,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ExpandMSBuildProperties(profile.EnvironmentVariables, evaluateExpression),
        };
}
