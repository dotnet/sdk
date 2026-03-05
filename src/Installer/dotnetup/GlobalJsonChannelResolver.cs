// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Shared utility for resolving the SDK channel (feature band) from a global.json file.
/// </summary>
internal static class GlobalJsonChannelResolver
{
    /// <summary>
    /// Reads a global.json file and derives the SDK feature band channel from the specified version.
    /// For example, a version of "10.0.105" yields channel "10.0.1xx".
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
            var json = File.ReadAllText(globalJsonPath);
            var contents = JsonSerializer.Deserialize(json, GlobalJsonContentsJsonContext.Default.GlobalJsonContents);

            if (contents?.Sdk?.Version is not { } versionString || string.IsNullOrWhiteSpace(versionString))
            {
                return null;
            }

            return DeriveFeatureBandChannel(versionString);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Derives the feature band channel from an SDK version string.
    /// For example, "10.0.105" → "10.0.1xx", "9.0.304" → "9.0.3xx".
    /// Returns the original string if it can't be parsed as a valid SDK version.
    /// </summary>
    internal static string? DeriveFeatureBandChannel(string versionString)
    {
        if (!ReleaseVersion.TryParse(versionString, out var version))
        {
            return null;
        }

        // SDK versions have patch >= 100; derive the feature band
        int featureBand = version.Patch / 100;
        return string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{featureBand}xx");
    }
}
