// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ProjectTools;

internal abstract partial class LaunchProfileParser
{
    [GeneratedRegex(@"\$\([^)]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex MSBuildPropertyRegex();

    public abstract LaunchProfileParseResult ParseProfile(
        string launchSettingsPath,
        string? launchProfileName,
        string json,
        Func<string, string>? expandMSBuildProperty = null);

    protected static string? ParseCommandLineArgs(string? value, Func<string, string>? expandMSBuildProperty)
        => value is not null ? ExpandVariables(value, expandMSBuildProperty) : null;

    public static string GetLaunchProfileDisplayName(string? launchProfile)
        => string.IsNullOrEmpty(launchProfile) ? Resources.DefaultLaunchProfileDisplayName : launchProfile;

    internal static bool RequiresMSBuildExpansion(string? value)
        => value is not null && MSBuildPropertyRegex().IsMatch(value);

    protected static ImmutableDictionary<string, string> ParseEnvironmentVariables(
        ImmutableDictionary<string, string> values,
        Func<string, string>? expandMSBuildProperty)
    {
        if (values.Count == 0)
        {
            return values;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            // override previously set variables:
            builder[key] = ExpandVariables(value, expandMSBuildProperty);
        }

        return builder.ToImmutable();
    }

    protected static string ExpandVariables(string value, Func<string, string>? expandMSBuildProperty)
    {
        string expandedValue = Environment.ExpandEnvironmentVariables(value);
        return expandMSBuildProperty is null
            ? expandedValue
            : MSBuildPropertyRegex().Replace(expandedValue, match => expandMSBuildProperty(match.Value));
    }
}
