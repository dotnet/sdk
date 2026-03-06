// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class UpdateChannel
{
    public string Name { get; }

    public UpdateChannel(string name)
    {
        Name = name;
    }

    public bool IsFullySpecifiedVersion()
    {
        return ReleaseVersion.TryParse(Name, out _);
    }

    /// <summary>
    /// Checks if the channel string looks like an SDK version or feature band pattern rather than a runtime version.
    /// SDK versions have a third component >= 100 (e.g., "9.0.103", "9.0.304") or use "xx" patterns (e.g., "9.0.1xx").
    /// Runtime versions have a third component &lt; 100 (e.g., "9.0.12", "9.0.0").
    /// </summary>
    /// <remarks>
    /// We cannot use ReleaseVersion.SdkFeatureBand here because ReleaseVersion parses any valid semantic version
    /// without knowing if it's an SDK or runtime version. For example, both "9.0.103" (SDK) and "9.0.12" (runtime)
    /// would parse successfully, but SdkFeatureBand would return 100 for the SDK version and 0 for the runtime version.
    /// Since we're validating user input where we don't know the intent, we use a heuristic: any third component >= 100
    /// or containing 'x' is likely an SDK version/feature band and should be rejected for runtime installations.
    /// </remarks>
    public bool IsSdkVersionOrFeatureBand()
    {
        var parts = Name.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        string thirdPart = parts[2];

        // Check for feature band patterns like "1xx", "2xx", "12x"
        if (thirdPart.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if it's a numeric SDK version (patch >= 100 indicates SDK, e.g., "9.0.103")
        // Runtime patches are typically < 100 (e.g., "9.0.12")
        if (int.TryParse(thirdPart, out int patch) && patch >= 100)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the given version matches this channel pattern.
    /// Supports exact versions, named channels (latest, lts, sts, preview),
    /// major-only, major.minor, and feature band patterns.
    /// </summary>
    public bool Matches(ReleaseVersion version)
    {
        if (string.IsNullOrEmpty(Name))
        {
            return false;
        }

        // Exact version match
        if (string.Equals(version.ToString(), Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Named channels
        if (Name.Equals("lts", StringComparison.OrdinalIgnoreCase))
        {
            // LTS releases are even major versions
            return version.Major % 2 == 0;
        }

        if (Name.Equals("latest", StringComparison.OrdinalIgnoreCase) ||
            Name.Equals("sts", StringComparison.OrdinalIgnoreCase) ||
            Name.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Major version match (e.g., "10" matches "10.0.103")
        if (int.TryParse(Name, out var major))
        {
            return version.Major == major;
        }

        var parts = Name.Split('.');

        // Major.Minor match (e.g., "10.0" matches "10.0.103")
        if (parts.Length == 2 && int.TryParse(parts[0], out var specMajor) && int.TryParse(parts[1], out var specMinor))
        {
            return version.Major == specMajor && version.Minor == specMinor;
        }

        // Feature band match (e.g., "10.0.1xx" matches "10.0.103")
        if (parts.Length == 3 && parts[2].EndsWith("xx", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(parts[0], out var fbMajor) && int.TryParse(parts[1], out var fbMinor))
            {
                var bandPrefix = parts[2].Substring(0, parts[2].Length - 2);
                if (int.TryParse(bandPrefix, out var band))
                {
                    return version.Major == fbMajor && version.Minor == fbMinor &&
                           version.Patch / 100 == band;
                }
            }
        }

        return false;
    }
}
