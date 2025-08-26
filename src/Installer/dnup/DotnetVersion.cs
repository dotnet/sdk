// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Represents the type of .NET version (SDK or Runtime).
/// </summary>
internal enum DotnetVersionType
{
    /// <summary>Automatically detect based on version format.</summary>
    Auto,
    /// <summary>SDK version (has feature bands, e.g., 8.0.301).</summary>
    Sdk,
    /// <summary>Runtime version (no feature bands, e.g., 8.0.7).</summary>
    Runtime
}

/// <summary>
/// Represents a .NET version string with specialized parsing, comparison, and manipulation capabilities.
/// Acts like a string but provides version-specific operations like feature band extraction and semantic comparisons.
/// Supports both SDK versions (with feature bands) and Runtime versions, and handles build hashes and preview versions.
/// </summary>
[DebuggerDisplay("{Value} ({VersionType})")]
internal readonly record struct DotnetVersion : IComparable<DotnetVersion>, IComparable<string>, IEquatable<string>
{
    private readonly ReleaseVersion? _releaseVersion;

    /// <summary>Gets the original version string value.</summary>
    public string Value { get; }

    /// <summary>Gets the version type (SDK or Runtime).</summary>
    public DotnetVersionType VersionType { get; }

    /// <summary>Gets the major version component (e.g., "8" from "8.0.301").</summary>
    public int Major => _releaseVersion?.Major ?? 0;

    /// <summary>Gets the minor version component (e.g., "0" from "8.0.301").</summary>
    public int Minor => _releaseVersion?.Minor ?? 0;

    /// <summary>Gets the patch version component (e.g., "301" from "8.0.301").</summary>
    public int Patch => _releaseVersion?.Patch ?? 0;

    /// <summary>Gets the major.minor version string (e.g., "8.0" from "8.0.301").</summary>
    public string MajorMinor => $"{Major}.{Minor}";

    /// <summary>Gets whether this version represents a preview version (contains '-preview').</summary>
    public bool IsPreview => Value.Contains("-preview", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets whether this version represents a prerelease (contains '-' but not just build hash).</summary>
    public bool IsPrerelease => Value.Contains('-') && !IsOnlyBuildHash();

    /// <summary>Gets whether this is an SDK version (has feature bands).</summary>
    public bool IsSdkVersion => VersionType == DotnetVersionType.Sdk || 
        (VersionType == DotnetVersionType.Auto && DetectVersionType() == DotnetVersionType.Sdk);

    /// <summary>Gets whether this is a Runtime version (no feature bands).</summary>
    public bool IsRuntimeVersion => VersionType == DotnetVersionType.Runtime ||
        (VersionType == DotnetVersionType.Auto && DetectVersionType() == DotnetVersionType.Runtime);

    /// <summary>Gets whether this version contains a build hash.</summary>
    public bool HasBuildHash => GetBuildHash() is not null;

    /// <summary>Gets whether this version is fully specified (e.g., "8.0.301" vs "8.0" or "8.0.3xx").</summary>
    public bool IsFullySpecified => _releaseVersion is not null &&
        !Value.Contains('x') &&
        Value.Split('.').Length >= 3;

    /// <summary>Gets whether this version uses a non-specific feature band pattern (e.g., "8.0.3xx").</summary>
    public bool IsNonSpecificFeatureBand => Value.EndsWith('x') && Value.Split('.').Length == 3;

    /// <summary>Gets whether this is just a major or major.minor version (e.g., "8" or "8.0").</summary>
    public bool IsNonSpecificMajorMinor => Value.Split('.').Length <= 2 &&
        Value.Split('.').All(x => int.TryParse(x, out _));

    /// <summary>
    /// Initializes a new instance with the specified version string.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="versionType">The type of version (SDK or Runtime). Auto-detects if not specified.</param>
    public DotnetVersion(string? value, DotnetVersionType versionType = DotnetVersionType.Auto)
    {
        Value = value ?? string.Empty;
        VersionType = versionType;
        _releaseVersion = ReleaseVersion.TryParse(GetVersionWithoutBuildHash(), out var version) ? version : null;
    }

    /// <summary>
    /// Gets the feature band number from the SDK version (e.g., "3" from "8.0.301").
    /// Returns null if this is not an SDK version or doesn't contain a feature band.
    /// </summary>
    public string? GetFeatureBand()
    {
        if (!IsSdkVersion) return null;
        
        var parts = GetVersionWithoutBuildHash().Split('.');
        if (parts.Length < 3) return null;

        var patchPart = parts[2].Split('-')[0]; // Remove prerelease suffix
        return patchPart.Length > 0 ? patchPart[0].ToString() : null;
    }

    /// <summary>
    /// Gets the feature band patch version (e.g., "01" from "8.0.301").
    /// Returns null if this is not an SDK version or doesn't contain a feature band.
    /// </summary>
    public string? GetFeatureBandPatch()
    {
        if (!IsSdkVersion) return null;
        
        var parts = GetVersionWithoutBuildHash().Split('.');
        if (parts.Length < 3) return null;

        var patchPart = parts[2].Split('-')[0]; // Remove prerelease suffix
        return patchPart.Length > 1 ? patchPart[1..] : null;
    }

    /// <summary>
    /// Gets the complete feature band including patch (e.g., "301" from "8.0.301").
    /// Returns null if this is not an SDK version or doesn't contain a feature band.
    /// </summary>
    public string? GetCompleteBandAndPatch()
    {
        if (!IsSdkVersion) return null;
        
        var parts = GetVersionWithoutBuildHash().Split('.');
        if (parts.Length < 3) return null;

        return parts[2].Split('-')[0]; // Remove prerelease suffix if present
    }

    /// <summary>
    /// Gets the prerelease identifier if this is a prerelease version.
    /// </summary>
    public string? GetPrereleaseIdentifier()
    {
        var dashIndex = Value.IndexOf('-');
        return dashIndex >= 0 ? Value[(dashIndex + 1)..] : null;
    }

    /// <summary>
    /// Gets the build hash from the version if present (typically after a '+' or at the end of prerelease).
    /// Examples: "8.0.301+abc123" -> "abc123", "8.0.301-preview.1.abc123" -> "abc123"
    /// </summary>
    public string? GetBuildHash()
    {
        // Build hash after '+'
        var plusIndex = Value.IndexOf('+');
        if (plusIndex >= 0)
            return Value[(plusIndex + 1)..];

        // Build hash in prerelease (look for hex-like string at the end)
        var prerelease = GetPrereleaseIdentifier();
        if (prerelease is null) return null;

        var parts = prerelease.Split('.');
        var lastPart = parts[^1];
        
        // Check if last part looks like a build hash (hex string, 6+ chars)
        if (lastPart.Length >= 6 && lastPart.All(c => char.IsAsciiHexDigit(c)))
            return lastPart;

        return null;
    }

    /// <summary>
    /// Gets the version string without any build hash component.
    /// </summary>
    public string GetVersionWithoutBuildHash()
    {
        var buildHash = GetBuildHash();
        if (buildHash is null) return Value;

        // Remove build hash after '+'
        var plusIndex = Value.IndexOf('+');
        if (plusIndex >= 0)
            return Value[..plusIndex];

        // Remove build hash from prerelease
        return Value.Replace($".{buildHash}", "");
    }

    /// <summary>
    /// Detects whether this is an SDK or Runtime version based on the version format.
    /// SDK versions typically have 3-digit patch numbers (feature bands), Runtime versions have 1-2 digit patch numbers.
    /// </summary>
    private DotnetVersionType DetectVersionType()
    {
        var parts = GetVersionWithoutBuildHash().Split('.', '-');
        if (parts.Length < 3) return DotnetVersionType.Runtime;

        var patchPart = parts[2];
        
        // SDK versions typically have 3-digit patch numbers (e.g., 301, 201)
        // Runtime versions have 1-2 digit patch numbers (e.g., 7, 12)
        if (patchPart.Length >= 3 && patchPart.All(char.IsDigit))
            return DotnetVersionType.Sdk;
            
        return DotnetVersionType.Runtime;
    }

    /// <summary>
    /// Checks if the version only contains a build hash (no other prerelease identifiers).
    /// </summary>
    private bool IsOnlyBuildHash()
    {
        var dashIndex = Value.IndexOf('-');
        if (dashIndex < 0) return false;

        var afterDash = Value[(dashIndex + 1)..];
        
        // Check if what follows the dash is just a build hash
        return afterDash.Length >= 6 && afterDash.All(c => char.IsAsciiHexDigit(c));
    }

    /// <summary>
    /// Creates a new version with the specified patch version while preserving other components.
    /// </summary>
    public DotnetVersion WithPatch(int patch)
    {
        var parts = Value.Split('.');
        if (parts.Length < 3)
            return new DotnetVersion($"{Major}.{Minor}.{patch:D3}");

        var prereleaseAndBuild = GetPrereleaseAndBuildSuffix();
        return new DotnetVersion($"{Major}.{Minor}.{patch:D3}{prereleaseAndBuild}");
    }

    /// <summary>
    /// Creates a new version with the specified feature band while preserving other components.
    /// </summary>
    public DotnetVersion WithFeatureBand(int featureBand)
    {
        var currentPatch = GetFeatureBandPatch();
        var patch = $"{featureBand}{currentPatch ?? "00"}";
        var prereleaseAndBuild = GetPrereleaseAndBuildSuffix();
        return new DotnetVersion($"{Major}.{Minor}.{patch}{prereleaseAndBuild}");
    }

    private string GetPrereleaseAndBuildSuffix()
    {
        var dashIndex = Value.IndexOf('-');
        return dashIndex >= 0 ? Value[dashIndex..] : string.Empty;
    }

    /// <summary>
    /// Validates that this version string represents a well-formed, fully specified version.
    /// </summary>
    public bool IsValidFullySpecifiedVersion()
    {
        if (!IsFullySpecified) return false;

        var parts = Value.Split('.', '-')[0].Split('.');
        if (parts.Length < 3 || Value.Length > 20) return false;

        // Check that patch version is reasonable (1-2 digits for feature band, 1-2 for patch)
        return parts.All(p => int.TryParse(p, out _)) && parts[2].Length is >= 2 and <= 3;
    }

    #region String-like behavior

    public static implicit operator string(DotnetVersion version) => version.Value;
    public static implicit operator DotnetVersion(string version) => new(version);

    /// <summary>
    /// Creates an SDK version from a string.
    /// </summary>
    public static DotnetVersion FromSdk(string version) => new(version, DotnetVersionType.Sdk);

    /// <summary>
    /// Creates a Runtime version from a string.
    /// </summary>
    public static DotnetVersion FromRuntime(string version) => new(version, DotnetVersionType.Runtime);

    public override string ToString() => Value;

    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.Ordinal);

    #endregion

    #region IComparable implementations

    public int CompareTo(DotnetVersion other)
    {
        // Use semantic version comparison if both are valid release versions
        if (_releaseVersion is not null && other._releaseVersion is not null)
            return _releaseVersion.CompareTo(other._releaseVersion);

        // Fall back to string comparison
        return string.Compare(Value, other.Value, StringComparison.Ordinal);
    }

    public int CompareTo(string? other)
    {
        if (other is null) return 1;
        return CompareTo(new DotnetVersion(other));
    }

    #endregion

    #region Static utility methods

    /// <summary>
    /// Determines whether the specified string represents a valid .NET version format.
    /// </summary>
    public static bool IsValidFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return new DotnetVersion(value).IsValidFullySpecifiedVersion() ||
               new DotnetVersion(value).IsNonSpecificFeatureBand ||
               new DotnetVersion(value).IsNonSpecificMajorMinor;
    }

    /// <summary>
    /// Tries to parse a version string into a DotnetVersion.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="version">The parsed version if successful.</param>
    /// <param name="versionType">The type of version to parse. Auto-detects if not specified.</param>
    public static bool TryParse(string? value, out DotnetVersion version, DotnetVersionType versionType = DotnetVersionType.Auto)
    {
        version = new DotnetVersion(value, versionType);
        return IsValidFormat(value);
    }

    /// <summary>
    /// Parses a version string into a DotnetVersion, throwing on invalid format.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="versionType">The type of version to parse. Auto-detects if not specified.</param>
    public static DotnetVersion Parse(string value, DotnetVersionType versionType = DotnetVersionType.Auto)
    {
        if (!TryParse(value, out var version, versionType))
            throw new ArgumentException($"'{value}' is not a valid .NET version format.", nameof(value));
        return version;
    }

    #endregion

    #region String comparison operators

    public static bool operator <(DotnetVersion left, DotnetVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(DotnetVersion left, DotnetVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(DotnetVersion left, DotnetVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(DotnetVersion left, DotnetVersion right) => left.CompareTo(right) >= 0;

    public static bool operator ==(DotnetVersion left, string? right) => left.Equals(right);
    public static bool operator !=(DotnetVersion left, string? right) => !left.Equals(right);
    public static bool operator ==(string? left, DotnetVersion right) => right.Equals(left);
    public static bool operator !=(string? left, DotnetVersion right) => !right.Equals(left);

    #endregion
}
