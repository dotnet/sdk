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
        var config = new ProjectLaunchSettingsModel
        {
            LaunchProfileName = launchProfileName
        };

        foreach (var property in model.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.ExecutablePath), StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetStringValue(property.Value, out var executablePathValue))
                {
                    return new LaunchSettingsApplyResult(false, string.Format(CliCommandStrings.CouldNotConvertToString, property.Name));
                }

                config.ExecutablePath = executablePathValue;
            }
            else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.CommandLineArgs), StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetStringValue(property.Value, out var commandLineArgsValue))
                {
                    return new LaunchSettingsApplyResult(false, string.Format(CliCommandStrings.CouldNotConvertToString, property.Name));
                }

                config.CommandLineArgs = commandLineArgsValue;
            }
            else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.WorkingDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetStringValue(property.Value, out var workingDirectoryValue))
                {
                    return new LaunchSettingsApplyResult(false, string.Format(CliCommandStrings.CouldNotConvertToString, property.Name));
                }

                config.WorkingDirectory = workingDirectoryValue;
            }
            else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.EnvironmentVariables), StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    return new LaunchSettingsApplyResult(false, string.Format(CliCommandStrings.ValueMustBeAnObject, property.Name));
                }

                foreach (var environmentVariable in property.Value.EnumerateObject())
                {
                    if (TryGetStringValue(environmentVariable.Value, out var environmentVariableValue))
                    {
                        config.EnvironmentVariables[environmentVariable.Name] = environmentVariableValue!;
                    }
                }
            }
        }

        return new LaunchSettingsApplyResult(true, null, config);
    }

    private static bool TryGetStringValue(JsonElement element, out string? value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                value = bool.TrueString;
                return true;
            case JsonValueKind.False:
                value = bool.FalseString;
                return true;
            case JsonValueKind.Null:
                value = string.Empty;
                return true;
            case JsonValueKind.Number:
                value = element.GetRawText();
                return false;
            case JsonValueKind.String:
                value = element.GetString();
                return true;
            default:
                value = null;
                return false;
        }
    }
}
