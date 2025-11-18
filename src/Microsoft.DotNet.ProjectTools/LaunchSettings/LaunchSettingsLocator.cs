// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.ProjectTools;

public static class LaunchSettingsLocator
{
    public static string GetPropertiesLaunchSettingsPath(string directoryPath, string propertiesDirectoryName)
        => Path.Combine(directoryPath, propertiesDirectoryName, "launchSettings.json");

    public static string GetFlatLaunchSettingsPath(string directoryPath, string projectNameWithoutExtension)
        => Path.Join(directoryPath, $"{projectNameWithoutExtension}.run.json");

    public static string? TryFindLaunchSettings(string projectOrEntryPointFilePath, string? launchProfile, Action<string, bool> report)
    {
        var buildPathContainer = Path.GetDirectoryName(projectOrEntryPointFilePath);
        Debug.Assert(buildPathContainer != null);

        // VB.NET projects store the launch settings file in the
        // "My Project" directory instead of a "Properties" directory.
        // TODO: use the `AppDesignerFolder` MSBuild property instead, which captures this logic already
        var propsDirectory = string.Equals(Path.GetExtension(projectOrEntryPointFilePath), ".vbproj", StringComparison.OrdinalIgnoreCase)
             ? "My Project"
             : "Properties";

        string launchSettingsPath = GetPropertiesLaunchSettingsPath(buildPathContainer, propsDirectory);
        bool hasLaunchSetttings = File.Exists(launchSettingsPath);

        string appName = Path.GetFileNameWithoutExtension(projectOrEntryPointFilePath);
        string runJsonPath = GetFlatLaunchSettingsPath(buildPathContainer, appName);
        bool hasRunJson = File.Exists(runJsonPath);

        if (hasLaunchSetttings)
        {
            if (hasRunJson)
            {
                report(string.Format(Resources.RunCommandWarningRunJsonNotUsed, runJsonPath, launchSettingsPath), false);
            }

            return launchSettingsPath;
        }

        if (hasRunJson)
        {
            return runJsonPath;
        }

        if (!string.IsNullOrEmpty(launchProfile))
        {
            report(string.Format(Resources.RunCommandExceptionCouldNotLocateALaunchSettingsFile, launchProfile, $"""
                    {launchSettingsPath}
                    {runJsonPath}
                    """), true);
        }

        return null;
    }
}
