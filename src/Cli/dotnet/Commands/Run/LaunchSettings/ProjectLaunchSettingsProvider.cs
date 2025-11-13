// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal class ProjectLaunchSettingsProvider : ILaunchSettingsProvider
{
    public static readonly string CommandNameValue = "Project";

    public static string CommandName => CommandNameValue;

    public LaunchSettingsApplyResult TryGetLaunchSettings(string? launchProfileName, JsonElement model)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<ProjectLaunchProfileJson>(model.GetRawText());
            if (profile == null)
            {
                return new LaunchSettingsApplyResult(false, CliCommandStrings.LaunchProfileIsNotAJsonObject);
            }

            var config = new ProjectLaunchSettingsModel
            {
                LaunchProfileName = launchProfileName,
                CommandLineArgs = profile.CommandLineArgs,
                LaunchBrowser = profile.LaunchBrowser,
                LaunchUrl = profile.LaunchUrl,
                ApplicationUrl = profile.ApplicationUrl,
                DotNetRunMessages = profile.DotNetRunMessages
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
