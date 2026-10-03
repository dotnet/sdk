// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ProjectTools;

internal readonly record struct LaunchProfileParserOptions(
    Func<string, string>? EvaluateExpression,
    bool ExpandProjectProfile,
    bool ExpandExecutableProfile,
    bool ExpandCommandLineArgs);

internal abstract partial class LaunchProfileParser
{
    // Keep launch profile expansion consistent with Visual Studio's DebugTokenReplacer.
    [GeneratedRegex(@"\$\([^)]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex MSBuildPropertyRegex();

    public abstract LaunchProfileParseResult ParseProfile(
        string launchSettingsPath,
        string? launchProfileName,
        string json,
        Func<string, string>? evaluateExpression,
        bool expandCommandLineArgs);

    protected static string? ParseCommandLineArgs(
        string? value,
        Func<string, string>? evaluateExpression,
        bool expandCommandLineArgs)
        => value is not null
            ? ExpandVariables(value, expandCommandLineArgs ? evaluateExpression : null)
            : null;

    protected static bool TryParseWorkingDirectory(
        string launchSettingsPath,
        string? value,
        Func<string, string>? evaluateExpression,
        out string? workingDirectory,
        [NotNullWhen(false)] out string? error)
    {
        if (value == null)
        {
            workingDirectory = null;
            error = null;
            return true;
        }

        var expandedValue = ExpandVariables(value, evaluateExpression);

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

    internal static bool RequiresMSBuildExpansion(string? value)
        => value is not null && MSBuildPropertyRegex().IsMatch(value);

    protected static ImmutableDictionary<string, string> ParseEnvironmentVariables(
        ImmutableDictionary<string, string> values,
        Func<string, string>? evaluateExpression)
    {
        if (values.Count == 0)
        {
            return values;
        }

        var builder = values.ToBuilder();
        foreach (var (key, value) in values)
        {
            builder[key] = ExpandVariables(value, evaluateExpression);
        }

        return builder.ToImmutable();
    }

    protected static ImmutableDictionary<string, string> ExpandMSBuildProperties(
        ImmutableDictionary<string, string> values,
        Func<string, string> evaluateExpression)
    {
        if (values.Count == 0)
        {
            return values;
        }

        var builder = values.ToBuilder();
        foreach (var (key, value) in values)
        {
            builder[key] = ExpandMSBuildProperties(value, evaluateExpression);
        }

        return builder.ToImmutable();
    }

    protected static string ExpandMSBuildProperties(string value, Func<string, string> evaluateExpression)
        => MSBuildPropertyRegex().Replace(value, match => evaluateExpression(match.Value));

    internal static string ExpandVariables(string value, Func<string, string>? evaluateExpression)
    {
        string expandedValue = Environment.ExpandEnvironmentVariables(value);
        return evaluateExpression is null
            ? expandedValue
            : ExpandMSBuildProperties(expandedValue, evaluateExpression);
    }
}
