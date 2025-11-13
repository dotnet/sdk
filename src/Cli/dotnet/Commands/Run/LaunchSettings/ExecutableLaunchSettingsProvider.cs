// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal class ExecutableLaunchSettingsProvider : ILaunchSettingsProvider
{
    public static readonly string CommandNameValue = "Executable";

    public static string CommandName => CommandNameValue;

    public LaunchSettingsApplyResult TryGetLaunchSettings(string? launchProfileName, JsonElement model)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<ExecutableLaunchProfileJson>(model.GetRawText());
            if (profile == null)
            {
                return new LaunchSettingsApplyResult(false, CliCommandStrings.LaunchProfileIsNotAJsonObject);
            }

            var config = new ExecutableLaunchSettingsModel
            {
                LaunchProfileName = launchProfileName,
                ExecutablePath = profile.ExecutablePath,
                CommandLineArgs = profile.CommandLineArgs,
                WorkingDirectory = profile.WorkingDirectory
            };

            if (profile.EnvironmentVariables != null)
            {
                foreach (var (key, value) in profile.EnvironmentVariables)
                {
                    config.EnvironmentVariables[key] = value;
                }
            }

            return new LaunchSettingsApplyResult(true, null, config);
        }
        catch (JsonException ex)
        {
            return new LaunchSettingsApplyResult(false, string.Format(CliCommandStrings.DeserializationExceptionMessage, launchProfileName ?? "profile", ex.Message));
        }
    }
}
