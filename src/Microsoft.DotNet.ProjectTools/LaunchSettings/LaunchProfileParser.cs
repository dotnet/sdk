// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ProjectTools;

internal abstract class LaunchProfileParser
{
    public abstract LaunchProfileParseResult ParseProfile(string launchSettingsPath, string? launchProfileName, string json);

    protected static string? ParseCommandLineArgs(string? value)
        => value != null ? ExpandVariables(value) : null;

    protected static bool TryParseWorkingDirectory(string launchSettingsPath, string? value, out string? workingDirectory, [NotNullWhen(false)] out string? error)
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
            error = string.Format(Resources.Path0SpecifiedIn1IsInvalid, expandedValue, LaunchProfile.WorkingDirectoryPropertyName);
            return false;
        }
    }

    public static string GetLaunchProfileDisplayName(string? launchProfile)
        => string.IsNullOrEmpty(launchProfile) ? Resources.DefaultLaunchProfileDisplayName : launchProfile;

    protected static ImmutableDictionary<string, string> ParseEnvironmentVariables(ImmutableDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return values;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            // override previously set variables:
            builder[key] = ExpandVariables(value);
        }

        return builder.ToImmutable();
    }

    protected static string ExpandVariables(string value)
        => Environment.ExpandEnvironmentVariables(value);
}
