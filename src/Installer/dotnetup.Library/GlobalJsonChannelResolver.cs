// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Shared utility for resolving the SDK channel (feature band) from a global.json file.
/// Takes into account the rollForward policy.
/// </summary>
internal static class GlobalJsonChannelResolver
{
    /// <summary>
    /// Reads a global.json file and derives the SDK channel from the specified version,
    /// respecting the rollForward policy.
    /// <para>
    /// Roll-forward mapping:
    /// <list type="bullet">
    /// <item><c>disable</c>, <c>patch</c>, <c>feature</c>, <c>minor</c>, <c>major</c> — pin to exact version</item>
    /// <item><c>latestPatch</c> (default) — feature band channel (e.g., <c>10.0.1xx</c>)</item>
    /// <item><c>latestFeature</c> — major.minor channel (e.g., <c>10.0</c>)</item>
    /// <item><c>latestMinor</c> — major-only channel (e.g., <c>10</c>)</item>
    /// <item><c>latestMajor</c> — <c>latest</c></item>
    /// </list>
    /// </para>
    /// Returns null if the file doesn't exist, can't be parsed, or doesn't specify an SDK version.
    /// </summary>
    public static string? ResolveChannel(string globalJsonPath)
    {
        if (!File.Exists(globalJsonPath))
        {
            return null;
        }

        try
        {
            using var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(globalJsonPath);
            var contents = JsonSerializer.Deserialize(stream, GlobalJsonContentsJsonContext.Default.GlobalJsonContents);

            if (contents?.Sdk?.Version is not { } versionString || string.IsNullOrWhiteSpace(versionString))
            {
                return null;
            }

            var rollForward = contents.Sdk.RollForward;
            return DeriveChannel(versionString, rollForward);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Derives the channel from an SDK version string and roll-forward policy.
    /// </summary>
    internal static string? DeriveChannel(string versionString, string? rollForward)
    {
        if (!ReleaseVersion.TryParse(versionString, out var version))
        {
            return null;
        }

        // For rollForward values without "latest", pin to the exact version
        return rollForward?.ToLowerInvariant() switch
        {
            "disable" or "patch" or "feature" or "minor" or "major" => versionString,
            "latestfeature" => string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}"),
            "latestminor" => string.Create(CultureInfo.InvariantCulture, $"{version.Major}"),
            "latestmajor" => "latest",
            // Default (null or "latestPatch") — feature band channel
            _ => DeriveFeatureBandChannel(version),
        };
    }

    /// <summary>
    /// Derives the feature band channel from a parsed SDK version.
    /// For example, "10.0.105" → "10.0.1xx", "9.0.304" → "9.0.3xx".
    /// </summary>
    internal static string DeriveFeatureBandChannel(ReleaseVersion version)
    {
        int featureBand = version.Patch / 100;
        return string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{featureBand}xx");
    }
}
