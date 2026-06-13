// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class UpdateChannel
{
    private const string DailySuffix = "-daily";
    private const string DailyKeyword = "daily";

    public string Name { get; }

    private static bool IsStableRelease(ReleaseVersion version) => string.IsNullOrEmpty(version.Prerelease);

    public UpdateChannel(string name)
    {
        Name = name;
    }

    public bool IsFullySpecifiedVersion()
    {
        return ReleaseVersion.TryParse(Name, out _);
    }

    /// <summary>
    /// True if this channel refers to a daily build — either bare <c>daily</c>
    /// or a scope with a <c>-daily</c> suffix (e.g. <c>10.0-daily</c>,
    /// <c>10.0.1xx-daily</c>).
    /// </summary>
    public bool IsDaily =>
        Name.Equals(DailyKeyword, StringComparison.OrdinalIgnoreCase) ||
        Name.EndsWith(DailySuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strips the <c>-daily</c> suffix from a channel name (e.g. <c>"10.0-daily"</c>
    /// → <c>"10.0"</c>). Returns the input unchanged if it doesn't end with
    /// <c>-daily</c>.
    /// </summary>
    public static string StripDailySuffix(string channelName)
        => channelName.EndsWith(DailySuffix, StringComparison.OrdinalIgnoreCase)
            ? channelName.Substring(0, channelName.Length - DailySuffix.Length)
            : channelName;

    /// <summary>
    /// Tries to split a daily-scope string into a partial-version prefix and a
    /// prerelease label. Accepts both <c>preview5</c> and <c>preview.5</c>
    /// spellings of the label (e.g. <c>"11.0.1xx-preview.5"</c> →
    /// <c>("11.0.1xx", "preview.5")</c>; <c>"11.0.1xx-preview5"</c> →
    /// <c>("11.0.1xx", "preview.5")</c>). The returned <paramref name="prereleaseLabel"/>
    /// is always normalized to <c>label.N</c> form (i.e. with the dot) so it can
    /// be compared directly against a <see cref="ReleaseVersion.Prerelease"/>.
    /// </summary>
    internal static bool TrySplitPartialVersionAndPrereleaseLabel(
        string scope,
        out string partialVersion,
        out string prereleaseLabel)
    {
        partialVersion = string.Empty;
        prereleaseLabel = string.Empty;

        int dashIndex = scope.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= scope.Length - 1)
        {
            return false;
        }

        string left = scope.Substring(0, dashIndex);
        string right = scope.Substring(dashIndex + 1);

        if (!TryNormalizePrereleaseLabel(right, out string normalized))
        {
            return false;
        }

        partialVersion = left;
        prereleaseLabel = normalized;
        return true;
    }

    /// <summary>
    /// Normalizes a prerelease label to <c>name.N</c> form (e.g.
    /// <c>"preview5"</c> and <c>"preview.5"</c> both produce <c>"preview.5"</c>).
    /// Returns <c>false</c> if the input doesn't match a <c>{letters}{digits}</c>
    /// or <c>{letters}.{digits}</c> shape.
    /// </summary>
    internal static bool TryNormalizePrereleaseLabel(string label, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrEmpty(label))
        {
            return false;
        }

        string name;
        string number;
        int dotIndex = label.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex >= 0)
        {
            name = label.Substring(0, dotIndex);
            number = label.Substring(dotIndex + 1);
        }
        else
        {
            // No dot: find the boundary between the alpha name and the digit run.
            int boundary = 0;
            while (boundary < label.Length && char.IsLetter(label[boundary]))
            {
                boundary++;
            }
            if (boundary == 0 || boundary == label.Length)
            {
                return false;
            }
            name = label.Substring(0, boundary);
            number = label.Substring(boundary);
        }

        if (name.Length == 0 || number.Length == 0)
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        foreach (char c in number)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        normalized = $"{name.ToLowerInvariant()}.{number}";
        return true;
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
    /// Supports exact versions, named channels (latest, lts, preview, daily),
    /// major-only, major.minor, and feature band patterns (each of these may
    /// also carry a <c>-daily</c> suffix to narrow the match to prerelease versions).
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

        // Named channels (lts, latest, preview, daily)
        if (TryMatchNamedChannel(version, out var versionMatchesChannel))
        {
            return versionMatchesChannel;
        }

        // Major version match (e.g., "10" matches "10.0.103")
        if (int.TryParse(Name, out var major))
        {
            return version.Major == major;
        }

        // Scoped daily channels (e.g. "10.0-daily", "10.0.1xx-daily",
        // "11.0.1xx-preview.5-daily") match the same versions their base scope
        // would, but restricted to prerelease versions. A stable release is not
        // a daily build, even if its version falls inside the base scope;
        // rejecting it here keeps the channel's "what satisfies me?" answer
        // coherent for GC and reporting.
        if (IsDaily)
        {
            if (IsStableRelease(version))
            {
                return false;
            }

            string scope = StripDailySuffix(Name);

            // Prerelease-qualified daily channels ("<band>-preview.5-daily") match only
            // versions whose prerelease starts with the requested label, in addition to the
            // base scope matching.
            if (TrySplitPartialVersionAndPrereleaseLabel(scope, out string partialVersion, out string prereleaseLabel))
            {
                if (!new UpdateChannel(partialVersion).Matches(version))
                {
                    return false;
                }

                return version.Prerelease.Equals(prereleaseLabel, StringComparison.OrdinalIgnoreCase)
                    || version.Prerelease.StartsWith(prereleaseLabel + ".", StringComparison.OrdinalIgnoreCase);
            }

            return new UpdateChannel(scope).Matches(version);
        }

        return MatchesMajorMinorOrFeatureBand(version);
    }

    /// <summary>
    /// Returns whether this channel is one of the recognized keyword channels
    /// (<c>lts</c>, <c>latest</c>, <c>preview</c>, <c>daily</c>). When it is,
    /// <paramref name="versionMatchesChannel"/> is set to whether <paramref name="version"/>
    /// satisfies that keyword's rules; when it isn't, this returns
    /// <c>false</c> so the caller can keep trying other match strategies.
    /// </summary>
    /// <remarks>
    /// The double-bool shape follows the standard <c>TryXxx</c> pattern: the
    /// return value answers "did the keyword path apply?" and
    /// <paramref name="versionMatchesChannel"/> carries the actual match decision when it did.
    /// </remarks>
    private bool TryMatchNamedChannel(ReleaseVersion version, out bool versionMatchesChannel)
    {
        if (Name.Equals("lts", StringComparison.OrdinalIgnoreCase))
        {
            // LTS releases are even major versions and must be stable releases.
            versionMatchesChannel = version.Major % 2 == 0 && IsStableRelease(version);
            return true;
        }

        // "latest" should only match stable releases so a preview SDK doesn't satisfy the
        // stable channel during garbage collection. "preview" continues to allow stable
        // matches so existing preview specs can keep a GA SDK when no preview exists yet.
        if (Name.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            versionMatchesChannel = IsStableRelease(version);
            return true;
        }

        if (Name.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            versionMatchesChannel = true;
            return true;
        }

        // Bare "daily" matches any prerelease version; stable releases are not daily builds.
        if (Name.Equals(DailyKeyword, StringComparison.OrdinalIgnoreCase))
        {
            versionMatchesChannel = !IsStableRelease(version);
            return true;
        }

        versionMatchesChannel = false;
        return false;
    }

    private bool MatchesMajorMinorOrFeatureBand(ReleaseVersion version)
    {
        var parts = Name.Split('.');

        // Major.Minor match (e.g., "10.0" matches "10.0.103")
        if (parts.Length == 2 && int.TryParse(parts[0], out var specMajor) && int.TryParse(parts[1], out var specMinor))
        {
            return version.Major == specMajor && version.Minor == specMinor;
        }

        // Feature band match (e.g., "10.0.1xx" matches "10.0.103")
        if (parts.Length == 3 && parts[2].EndsWith("xx", StringComparison.OrdinalIgnoreCase))
        {
            return MatchesFeatureBand(parts, version);
        }

        return false;
    }

    private static bool MatchesFeatureBand(string[] parts, ReleaseVersion version)
    {
        if (!int.TryParse(parts[0], out var fbMajor) || !int.TryParse(parts[1], out var fbMinor))
        {
            return false;
        }

        var bandPrefix = parts[2].Substring(0, parts[2].Length - 2);
        if (!int.TryParse(bandPrefix, out var band))
        {
            return false;
        }

        return version.Major == fbMajor && version.Minor == fbMinor &&
               version.Patch / 100 == band;
    }
}
