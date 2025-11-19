// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal sealed class ExecutableLaunchSettingsParser : LaunchProfileParser
{
    public const string CommandName = "Executable";

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

        return LaunchProfileSettings.Success(new ExecutableLaunchSettingsModel
        {
            LaunchSettingsPath = launchSettingsPath,
            LaunchProfileName = launchProfileName,
            ExecutablePath = profile.ExecutablePath,
            CommandLineArgs = profile.CommandLineArgs,
            WorkingDirectory = profile.WorkingDirectory,
            DotNetRunMessages = ParseDotNetRunMessages(profile.DotNetRunMessages),
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables),
        });
    }
}
