// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal sealed class ExecutableLaunchSettingsParser : LaunchProfileParser
{
    public const string CommandName = "Executable";

    public static readonly ExecutableLaunchSettingsParser Instance = new();

    private ExecutableLaunchSettingsParser()
    {
    }

    public override LaunchProfileSettings ParseProfile(string launchSettingsPath, string? launchProfileName, string json)
    {
        var profile = JsonSerializer.Deserialize<ExecutableLaunchProfileJson>(json);
        if (profile == null)
        {
            return LaunchProfileSettings.Failure(CliCommandStrings.LaunchProfileIsNotAJsonObject);
        }

        if (profile.ExecutablePath == null)
        {
            return LaunchProfileSettings.Failure(
                string.Format(
                    CliCommandStrings.LaunchProfile0IsMissingProperty1,
                    RunCommand.GetLaunchProfileDisplayName(launchProfileName),
                    ExecutableLaunchSettingsModel.ExecutablePathPropertyName));
        }

        if (!TryParseWorkingDirectory(launchSettingsPath, profile.WorkingDirectory, out var workingDirectory, out var error))
        {
            return LaunchProfileSettings.Failure(error);
        }

        return LaunchProfileSettings.Success(new ExecutableLaunchSettingsModel
        {
            LaunchProfileName = launchProfileName,
            ExecutablePath = ExpandVariables(profile.ExecutablePath),
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs),
            WorkingDirectory = workingDirectory,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables),
        });
    }

    private static bool TryParseWorkingDirectory(string launchSettingsPath, string? value, out string? workingDirectory, [NotNullWhen(false)] out string? error)
    {
        if (value == null)
        {
            workingDirectory = null;
            error = null;
            return true;
        }

        var expandedValue = ExpandVariables(value);

        try
        {
            workingDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(launchSettingsPath)!, expandedValue));
            error = null;
            return true;
        }
        catch
        {
            workingDirectory = null;
            error = string.Format(CliCommandStrings.Path0SpecifiedIn1IsInvalid, expandedValue, ExecutableLaunchSettingsModel.WorkingDirectoryPropertyName);
            return false;
        }
    }
}
