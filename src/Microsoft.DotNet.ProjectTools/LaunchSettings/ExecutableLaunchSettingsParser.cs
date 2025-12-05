// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.ProjectTools;

internal sealed class ExecutableLaunchSettingsParser : LaunchProfileParser
{
    private sealed class Json
    {
        [JsonPropertyName("commandName")]
        public string? CommandName { get; set; }

        [JsonPropertyName("executablePath")]
        public string? ExecutablePath { get; set; }

        [JsonPropertyName("commandLineArgs")]
        public string? CommandLineArgs { get; set; }

        [JsonPropertyName("workingDirectory")]
        public string? WorkingDirectory { get; set; }

        [JsonPropertyName("dotnetRunMessages")]
        public bool DotNetRunMessages { get; set; }

        [JsonPropertyName("environmentVariables")]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
    }

    public const string CommandName = "Executable";

    public static readonly ExecutableLaunchSettingsParser Instance = new();

    private ExecutableLaunchSettingsParser()
    {
    }

    public override LaunchProfileSettings ParseProfile(string launchSettingsPath, string? launchProfileName, string json)
    {
        var profile = JsonSerializer.Deserialize<Json>(json);
        if (profile == null)
        {
            return LaunchProfileSettings.Failure(Resources.LaunchProfileIsNotAJsonObject);
        }

        if (profile.ExecutablePath == null)
        {
            return LaunchProfileSettings.Failure(
                string.Format(
                    Resources.LaunchProfile0IsMissingProperty1,
                    LaunchProfileParser.GetLaunchProfileDisplayName(launchProfileName),
                    ExecutableLaunchSettings.ExecutablePathPropertyName));
        }

        if (!TryParseWorkingDirectory(launchSettingsPath, profile.WorkingDirectory, out var workingDirectory, out var error))
        {
            return LaunchProfileSettings.Failure(error);
        }

        return LaunchProfileSettings.Success(new ExecutableLaunchSettings
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
            error = string.Format(Resources.Path0SpecifiedIn1IsInvalid, expandedValue, ExecutableLaunchSettings.WorkingDirectoryPropertyName);
            return false;
        }
    }
}
