// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
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
        Func<string, string>? expandMSBuildProperty,
        bool expandCommandLineArgs)
    {
        var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonSerializerContext.Default.ExecutableLaunchProfile);
        if (profile == null)
        {
            return LaunchProfileParseResult.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        if (!TryParseWorkingDirectory(launchSettingsPath, profile.WorkingDirectory, expandMSBuildProperty, out var workingDirectory, out var error))
        {
            return LaunchProfileParseResult.Failure(error);
        }

        return LaunchProfileParseResult.Success(new ExecutableLaunchProfile
        {
            LaunchProfileName = launchProfileName,
            ExecutablePath = ExpandVariables(profile.ExecutablePath, expandMSBuildProperty),
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs, expandMSBuildProperty, expandCommandLineArgs),
            WorkingDirectory = workingDirectory,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables, expandMSBuildProperty),
        });
    }

    internal static bool RequiresMSBuildExpansion(ExecutableLaunchProfile profile, bool includeCommandLineArgs)
        => LaunchProfileParser.RequiresMSBuildExpansion(profile.ExecutablePath)
            || LaunchProfileParser.RequiresMSBuildExpansion(profile.WorkingDirectory)
            || (includeCommandLineArgs && LaunchProfileParser.RequiresMSBuildExpansion(profile.CommandLineArgs))
            || profile.EnvironmentVariables.Values.Any(LaunchProfileParser.RequiresMSBuildExpansion);

    private static bool TryParseWorkingDirectory(
        string launchSettingsPath,
        string? value,
        Func<string, string>? expandMSBuildProperty,
        out string? workingDirectory,
        [NotNullWhen(false)] out string? error)
    {
        if (value == null)
        {
            workingDirectory = null;
            error = null;
            return true;
        }

        var expandedValue = ExpandVariables(value, expandMSBuildProperty);

        try
        {
            workingDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(launchSettingsPath)!, expandedValue));
            error = null;
            return true;
        }
        catch
        {
            workingDirectory = null;
            error = string.Format(Resources.Path0SpecifiedIn1IsInvalid, expandedValue, ExecutableLaunchProfile.WorkingDirectoryPropertyName);
            return false;
        }
    }
}
