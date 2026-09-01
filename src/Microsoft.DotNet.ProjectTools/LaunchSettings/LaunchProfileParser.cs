// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ProjectTools;

internal abstract class LaunchProfileParser
{
    public abstract LaunchProfileParseResult ParseProfile(string launchSettingsPath, string? launchProfileName, string json);

    protected static string? ParseCommandLineArgs(string? value)
        => value != null ? ExpandVariables(value) : null;

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
