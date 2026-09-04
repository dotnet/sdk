// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ProjectTools;

internal sealed class ExecutableLaunchProfileParser : LaunchProfileParser
{
    public const string CommandName = "Executable";

    public static readonly ExecutableLaunchProfileParser Instance = new();

    private ExecutableLaunchProfileParser()
    {
    }

    public override LaunchProfileParseResult ParseProfile(
        string launchSettingsPath,
        string? launchProfileName,
        string json,
        Func<string, string>? evaluateExpression,
        bool expandCommandLineArgs)
    {
        var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonSerializerContext.Default.ExecutableLaunchProfile);
        if (profile == null)
        {
            return LaunchProfileParseResult.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        if (!TryParseWorkingDirectory(launchSettingsPath, profile.WorkingDirectory, evaluateExpression, out var workingDirectory, out var error))
        {
            return LaunchProfileParseResult.Failure(error);
        }

        return LaunchProfileParseResult.Success(new ExecutableLaunchProfile
        {
            LaunchProfileName = launchProfileName,
            ExecutablePath = ExpandVariables(profile.ExecutablePath, evaluateExpression),
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs, evaluateExpression, expandCommandLineArgs),
            WorkingDirectory = workingDirectory,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables, evaluateExpression),
        });
    }

    internal static bool RequiresMSBuildExpansion(ExecutableLaunchProfile profile, bool includeCommandLineArgs)
        => LaunchProfileParser.RequiresMSBuildExpansion(profile.ExecutablePath)
            || LaunchProfileParser.RequiresMSBuildExpansion(profile.WorkingDirectory)
            || (includeCommandLineArgs && LaunchProfileParser.RequiresMSBuildExpansion(profile.CommandLineArgs))
            || profile.EnvironmentVariables.Values.Any(LaunchProfileParser.RequiresMSBuildExpansion);
}
