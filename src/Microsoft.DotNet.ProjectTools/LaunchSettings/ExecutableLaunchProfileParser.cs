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

    public override LaunchProfileParseResult ParseProfile(string launchSettingsPath, string? launchProfileName, string json)
    {
        var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonSerializerContext.Default.ExecutableLaunchProfile);
        if (profile == null)
        {
            return LaunchProfileParseResult.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        if (!TryParseWorkingDirectory(launchSettingsPath, profile.WorkingDirectory, out var workingDirectory, out var error))
        {
            return LaunchProfileParseResult.Failure(error);
        }

        return LaunchProfileParseResult.Success(new ExecutableLaunchProfile
        {
            LaunchProfileName = launchProfileName,
            ExecutablePath = ExpandVariables(profile.ExecutablePath),
            CommandLineArgs = ParseCommandLineArgs(profile.CommandLineArgs),
            WorkingDirectory = workingDirectory,
            DotNetRunMessages = profile.DotNetRunMessages,
            EnvironmentVariables = ParseEnvironmentVariables(profile.EnvironmentVariables),
        });
    }
}
