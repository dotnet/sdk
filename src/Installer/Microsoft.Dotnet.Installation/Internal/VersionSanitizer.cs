// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Sanitizes version/channel strings for telemetry to prevent PII leakage.
/// Only known safe patterns are passed through; unknown patterns are replaced with "invalid".
/// </summary>
public static partial class VersionSanitizer
{
    /// <summary>
    /// Known safe channel keywords (sourced from ChannelVersionResolver).
    /// </summary>
    private static readonly HashSet<string> SafeKeywords = new(ChannelVersionResolver.KnownChannelKeywords, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Known safe prerelease tokens that can appear after a hyphen in version strings.
    /// These are standard .NET prerelease identifiers.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownPrereleaseTokens = ["preview", "rc", "rtm", "ga", "alpha", "beta", "dev", "ci", "servicing"];

    /// <summary>
    /// Regex to detect wildcard patterns: single digit followed by xx, or two digits followed by x.
    /// Examples: 1xx, 2xx, 10x, 20x
    /// Invalid patterns (all wildcards, too many x's, etc.) will fail ReleaseVersion parse after substitution.
    /// </summary>
    [GeneratedRegex(@"^(\d{1,2})(\.\d{1,2})\.(\d{1}xx|\d{2}x)$")]
    private static partial Regex WildcardPatternRegex();

    /// <summary>
    /// Sanitizes a version or channel string for safe telemetry collection.
    /// </summary>
    /// <param name="versionOrChannel">The raw version or channel string from user input.</param>
    /// <returns>The sanitized string, or "invalid" if the pattern is not recognized.</returns>
    public static string Sanitize(string? versionOrChannel)
    {
        if (string.IsNullOrWhiteSpace(versionOrChannel))
        {
            return "unspecified";
        }

        var trimmed = versionOrChannel.Trim();

        // Check for known safe keywords
        if (SafeKeywords.Contains(trimmed))
        {
            return trimmed.ToLowerInvariant();
        }

        // Check for valid version pattern
        if (IsValidVersionPattern(trimmed))
        {
            return trimmed;
        }

        // Unknown pattern - could contain PII, obfuscate it
        return "invalid";
    }

    /// <summary>
    /// Checks if a version string matches a valid pattern using ReleaseVersion parsing.
    /// For wildcard patterns (1xx, 10x), substitutes wildcards with '00' and validates.
    /// </summary>
    private static bool IsValidVersionPattern(string version)
    {
        // First check for wildcard patterns (e.g., 9.0.1xx, 10.0.20x)
        if (WildcardPatternRegex().IsMatch(version))
        {
            // Substitute wildcards with '00' to create a parseable version
            // 9.0.1xx -> 9.0.100, 10.0.20x -> 10.0.200
            var normalized = version
                .Replace("xx", "00", StringComparison.OrdinalIgnoreCase)
                .Replace("x", "0", StringComparison.OrdinalIgnoreCase);

            return ReleaseVersion.TryParse(normalized, out _);
        }

        if (ReleaseVersion.TryParse(version, out ReleaseVersion releaseVersion))
        {
            if (releaseVersion.Prerelease is null)
            {
                return true;
            }

            // Validate prerelease token: must start with a known token
            var dotIndex = releaseVersion.Prerelease.IndexOf('.');
            var token = dotIndex < 0 ? releaseVersion.Prerelease : releaseVersion.Prerelease[..dotIndex];

            return KnownPrereleaseTokens.Contains(token, StringComparer.OrdinalIgnoreCase);
        }

        // Check for partial versions like "8" or "8.0" which ReleaseVersion may not parse
        var parts = version.Split('.');
        if (parts.Length <= 2 && parts.All(p => int.TryParse(p, out var n) && n >= 0 && n < 100))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a version/channel string matches a known safe pattern.
    /// </summary>
    /// <param name="versionOrChannel">The version or channel string to check.</param>
    /// <returns>True if the pattern is recognized as safe.</returns>
    public static bool IsSafePattern(string? versionOrChannel)
    {
        if (string.IsNullOrWhiteSpace(versionOrChannel))
        {
            return true; // Empty is safe (will be reported as "unspecified")
        }

        var trimmed = versionOrChannel.Trim();
        return SafeKeywords.Contains(trimmed) || IsValidVersionPattern(trimmed);
    }
}
