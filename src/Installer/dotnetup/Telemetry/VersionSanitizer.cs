// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

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
    /// Regex pattern for valid version formats (without prerelease suffix):
    /// - Major only: 8, 9, 10
    /// - Major.Minor: 8.0, 9.0, 10.0
    /// - Feature band wildcard: 8.0.1xx, 9.0.3xx, 10.0.1xx (single digit + xx)
    /// - Single digit wildcard: 10.0.10x, 10.0.20x (two digits + single x)
    /// - Specific version: 8.0.100, 9.0.304
    /// Note: Patch versions are max 3 digits (100-999), so wildcards are constrained:
    ///   - Nxx pattern: single digit (1-9) + xx for feature bands (100-999)
    ///   - NNx pattern: two digits (10-99) + single x for narrower ranges (100-999)
    /// </summary>
    [GeneratedRegex(@"^(\d{1,2})(\.\d{1,2})?(\.\d{1,3}|\.\d{1}xx|\.\d{2}x)?$")]
    private static partial Regex BaseVersionPatternRegex();

    /// <summary>
    /// Regex pattern for prerelease suffix: hyphen followed by numbers and dots only.
    /// Example: -1.24234.5 (the token part like "preview" is validated separately)
    /// </summary>
    [GeneratedRegex(@"^(\.\d+)+$")]
    private static partial Regex PrereleaseSuffixRegex();

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
    /// Checks if a version string matches a valid pattern.
    /// </summary>
    private static bool IsValidVersionPattern(string version)
    {
        // Check if there's a prerelease suffix (contains hyphen)
        var hyphenIndex = version.IndexOf('-');
        if (hyphenIndex < 0)
        {
            // No prerelease suffix, just validate the base version
            return BaseVersionPatternRegex().IsMatch(version);
        }

        // Split into base version and prerelease parts
        var baseVersion = version[..hyphenIndex];
        var prereleasePart = version[(hyphenIndex + 1)..];

        // Validate base version
        if (!BaseVersionPatternRegex().IsMatch(baseVersion))
        {
            return false;
        }

        // Validate prerelease part: must start with a known token
        // Format: token[.number]* (e.g., "preview", "preview.1", "preview.1.24234.5", "rc.1")
        var dotIndex = prereleasePart.IndexOf('.');
        string token;
        string? suffix;

        if (dotIndex < 0)
        {
            token = prereleasePart;
            suffix = null;
        }
        else
        {
            token = prereleasePart[..dotIndex];
            suffix = prereleasePart[dotIndex..]; // Includes the leading dot
        }

        // Token must be a known prerelease identifier
        if (!KnownPrereleaseTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // If there's a suffix, it must be numbers separated by dots
        if (suffix != null && !PrereleaseSuffixRegex().IsMatch(suffix))
        {
            return false;
        }

        return true;
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
