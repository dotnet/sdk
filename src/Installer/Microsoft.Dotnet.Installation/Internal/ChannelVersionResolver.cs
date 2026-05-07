// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class ChannelVersionResolver
{
    /// <summary>
    /// Channel keyword for the latest stable release.
    /// </summary>
    public const string LatestChannel = "latest";

    /// <summary>
    /// Channel keyword for the latest preview release.
    /// </summary>
    public const string PreviewChannel = "preview";

    /// <summary>
    /// Channel keyword for the latest Long Term Support (LTS) release.
    /// </summary>
    public const string LtsChannel = "lts";

    /// <summary>
    /// Channel keyword for the latest daily build (latest major version).
    /// </summary>
    public const string DailyChannel = "daily";

    /// <summary>
    /// Suffix that turns any version scope channel into a daily-build channel
    /// (e.g. <c>10.0-daily</c>, <c>10.0.1xx-daily</c>).
    /// </summary>
    public const string DailySuffix = "-daily";

    /// <summary>
    /// Known channel keywords that are always valid.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownChannelKeywords = [LatestChannel, PreviewChannel, LtsChannel, DailyChannel];

    /// <summary>
    /// Maximum reasonable major version number. .NET versions are currently single-digit;
    /// anything above 99 is clearly invalid input (e.g., typos, random numbers).
    /// </summary>
    internal const int MaxReasonableMajorVersion = 99;

    private readonly ReleaseManifest _releaseManifest = new();
    private DailyChannelResolver? _dailyChannelResolver;

    public ChannelVersionResolver()
    {

    }

    public ChannelVersionResolver(ReleaseManifest releaseManifest)
    {
        _releaseManifest = releaseManifest;
    }

    public ChannelVersionResolver(ReleaseManifest releaseManifest, DailyChannelResolver dailyChannelResolver)
    {
        _releaseManifest = releaseManifest;
        _dailyChannelResolver = dailyChannelResolver;
    }

    public IEnumerable<string> GetSupportedChannels(bool includeFeatureBands = true)
    {
        var productIndex = _releaseManifest.GetReleasesIndex();
        return [..KnownChannelKeywords,
            ..productIndex
                .Where(p => p.IsSupported)
                .OrderByDescending(p => p.LatestReleaseVersion)
                .SelectMany(p => GetChannelsForProduct(p, includeFeatureBands))
        ];

        static IEnumerable<string> GetChannelsForProduct(Product product, bool includeFeatureBands)
        {
            if (!includeFeatureBands)
            {
                return [product.ProductVersion];
            }

            return [product.ProductVersion,
                ..product.GetReleasesAsync().GetAwaiter().GetResult()
                    .SelectMany(r => r.Sdks)
                    .Select(sdk => sdk.Version)
                    .OrderByDescending(v => v)
                    .Select(v => $"{v.Major}.{v.Minor}.{(v.Patch / 100)}xx")
                    .Distinct()
                    .ToList()
                ];
        }

    }

    public ReleaseVersion? Resolve(DotnetInstallRequest installRequest)
    {
        if (installRequest.Channel.IsDaily)
        {
            _dailyChannelResolver ??= new DailyChannelResolver(_releaseManifest);
            return _dailyChannelResolver.Resolve(installRequest.Channel, installRequest.InstallRoot.Architecture);
        }

        return GetLatestVersionForChannel(installRequest.Channel, installRequest.Component);
    }

    /// <summary>
    /// Checks if a channel string looks like a valid .NET version/channel format.
    /// This is a preliminary validation before attempting resolution.
    /// </summary>
    /// <param name="channel">The channel string to validate</param>
    /// <returns>True if the format appears valid, false if clearly invalid</returns>
    public static bool IsValidChannelFormat(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        // Known keywords are always valid (includes bare "daily").
        if (KnownChannelKeywords.Any(k => string.Equals(k, channel, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // For "<scope>-daily" channels, validate the scope as a version-like
        // string. Daily applies only to scopes (major / major.minor / feature
        // band) — fully specified versions like "10.0.103-daily" are rejected
        // because there's no such thing as "the daily 10.0.103" (a specific
        // patch is already specific).
        if (channel.EndsWith(DailySuffix, StringComparison.OrdinalIgnoreCase))
        {
            var scope = channel.Substring(0, channel.Length - DailySuffix.Length);
            if (string.IsNullOrEmpty(scope))
            {
                return false;
            }

            return IsValidScopeFormat(scope);
        }

        return IsValidScopeFormat(channel) || IsValidPrereleaseVersion(channel);
    }

    /// <summary>
    /// Validates a version-scope channel string: bare major (e.g. <c>10</c>),
    /// major.minor (e.g. <c>10.0</c>), or feature band (e.g. <c>10.0.1xx</c>).
    /// Rejects fully-specified versions and prerelease tags.
    /// </summary>
    private static bool IsValidScopeFormat(string scope)
    {
        var parts = scope.Split('.');
        if (parts.Length is 0 or > 3)
        {
            return false;
        }

        // First part must be a valid major version
        if (!int.TryParse(parts[0], out var major) || major < 0 || major > MaxReasonableMajorVersion)
        {
            return false;
        }

        // If there are more parts, validate them
        if (parts.Length >= 2)
        {
            if (!int.TryParse(parts[1], out var minor) || minor < 0)
            {
                return false;
            }
        }

        if (parts.Length == 3)
        {
            // Scope only allows feature band patterns like "1xx", not numeric patches.
            var patch = parts[2];
            if (!patch.EndsWith("xx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var prefix = patch.Substring(0, patch.Length - 2);
            if (prefix.Length == 0 || !int.TryParse(prefix, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a fully-specified prerelease version like <c>10.0.100-preview.1.32640</c>.
    /// </summary>
    private static bool IsValidPrereleaseVersion(string channel)
    {
        var dashIndex = channel.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex < 0)
        {
            // Non-prerelease, non-scope version (e.g. "10.0.103") — historically accepted.
            return IsValidNonScopeVersion(channel);
        }

        var versionPart = channel.Substring(0, dashIndex);
        return IsValidNonScopeVersion(versionPart);
    }

    /// <summary>
    /// Validates a numeric major[.minor[.patch]] version (no feature band, no prerelease).
    /// </summary>
    private static bool IsValidNonScopeVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length is 0 or > 4)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) || major < 0 || major > MaxReasonableMajorVersion)
        {
            return false;
        }

        if (parts.Length >= 2 && (!int.TryParse(parts[1], out var minor) || minor < 0))
        {
            return false;
        }

        if (parts.Length >= 3 && (!int.TryParse(parts[2], out var patch) || patch < 0))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a version channel string into its components.
    /// </summary>
    /// <param name="channel">Channel string to parse (e.g., "9", "9.0", "9.0.1xx", "9.0.103")</param>
    /// <returns>Tuple containing (major, minor, featureBand, isFullySpecified)</returns>
    private static (int Major, int Minor, string? FeatureBand, bool IsFullySpecified) ParseVersionChannel(UpdateChannel channel)
    {
        var parts = channel.Name.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : -1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : -1;

        // Check if we have a feature band (like 1xx) or a fully specified patch
        string? featureBand = null;
        bool isFullySpecified = false;

        if (parts.Length >= 3)
        {
            if (parts[2].EndsWith("xx", StringComparison.OrdinalIgnoreCase))
            {
                // Feature band pattern (e.g., "1xx")
                featureBand = parts[2].Substring(0, parts[2].Length - 2);
            }
            else if (int.TryParse(parts[2], out _))
            {
                // Fully specified version (e.g., "9.0.103")
                isFullySpecified = true;
            }
        }

        return (major, minor, featureBand, isFullySpecified);
    }

    /// <summary>
    /// Finds the latest fully specified version for a given channel string (major, major.minor, or feature band).
    /// </summary>
    /// <param name="channel">Channel string (e.g., "9", "9.0", "9.0.1xx", "9.0.103", "lts", "preview")</param>
    /// <param name="component">The component to check (ie SDK or runtime)</param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public ReleaseVersion? GetLatestVersionForChannel(UpdateChannel channel, InstallComponent component)
    {
        // Daily channels must be resolved via DailyChannelResolver. Reaching this method with a
        // daily channel means a caller bypassed Resolve(); refuse rather than silently parsing
        // "10.0.1xx-daily" as 10.0.x and returning the latest released version.
        if (channel.IsDaily)
        {
            return null;
        }

        if (string.Equals(channel.Name, LtsChannel, StringComparison.OrdinalIgnoreCase))
        {
            var productIndex = _releaseManifest.GetReleasesIndex();
            return GetLatestVersionByReleaseType(productIndex, ReleaseType.LTS, component);
        }
        else if (string.Equals(channel.Name, PreviewChannel, StringComparison.OrdinalIgnoreCase))
        {
            var productIndex = _releaseManifest.GetReleasesIndex();
            return GetLatestPreviewVersion(productIndex, component);
        }
        else if (string.Equals(channel.Name, LatestChannel, StringComparison.OrdinalIgnoreCase))
        {
            var productIndex = _releaseManifest.GetReleasesIndex();
            return GetLatestActiveVersion(productIndex, component);
        }

        var (major, minor, featureBand, isFullySpecified) = ParseVersionChannel(channel);

        // If major is invalid, return null
        if (major < 0)
        {
            return null;
        }

        // If the version is already fully specified, just return it as-is
        if (isFullySpecified)
        {
            return new ReleaseVersion(channel.Name);
        }

        // Load the index manifest
        var index = _releaseManifest.GetReleasesIndex();
        if (minor < 0)
        {
            return GetLatestVersionForMajorOrMajorMinor(index, major, component); // Major Only (e.g., "9")
        }
        else if (minor >= 0 && featureBand == null) // Major.Minor (e.g., "9.0")
        {
            return GetLatestVersionForMajorOrMajorMinor(index, major, component, minor);
        }
        else if (minor >= 0 && featureBand is not null) // Not Fully Qualified Feature band Version (e.g., "9.0.1xx")
        {
            return GetLatestVersionForFeatureBand(index, major, minor, featureBand, component);
        }

        return null;
    }

    private static IEnumerable<Product> GetProductsInMajorOrMajorMinor(IEnumerable<Product> index, int major, int? minor = null)
    {
        var validProducts = index.Where(p => minor is not null ? p.ProductVersion.Equals($"{major}.{minor}", StringComparison.Ordinal) : p.ProductVersion.StartsWith($"{major}.", StringComparison.Ordinal));
        return validProducts;
    }

    /// <summary>
    /// Gets the latest version for a major-only channel (e.g., "9").
    /// </summary>
    private static ReleaseVersion? GetLatestVersionForMajorOrMajorMinor(IEnumerable<Product> index, int major, InstallComponent component, int? minor = null)
    {
        // Assumption: The manifest is designed so that the first product for a major version will always be latest.
        Product? latestProductWithMajor = GetProductsInMajorOrMajorMinor(index, major, minor).FirstOrDefault();
        return GetLatestReleaseVersionInProduct(latestProductWithMajor, component);
    }

    /// <summary>
    /// Gets the latest version based on support status (LTS or STS).
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="releaseType">The release type to filter by (LTS or STS)</param>
    /// <param name="component">The component to check (ie SDK or runtime)</param>
    /// <returns>Latest stable version string matching the support status, or null if none found</returns>
    private static ReleaseVersion? GetLatestVersionByReleaseType(IEnumerable<Product> index, ReleaseType releaseType, InstallComponent component)
    {
        var correctPhaseProducts = index?.Where(p => p.ReleaseType == releaseType) ?? Enumerable.Empty<Product>();
        return GetLatestActiveVersion(correctPhaseProducts, component);
    }

    /// <summary>
    /// Gets the latest preview version available.
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="component">The component to check (ie SDK or runtime)</param>
    /// <returns>Latest preview or GoLive version string, or null if none found</returns>
    private static ReleaseVersion? GetLatestPreviewVersion(IEnumerable<Product> index, InstallComponent component)
    {
        ReleaseVersion? latestPreviewVersion = GetLatestVersionBySupportPhase(index, component, [SupportPhase.Preview, SupportPhase.GoLive]);
        if (latestPreviewVersion is not null)
        {
            return latestPreviewVersion;
        }

        return GetLatestVersionBySupportPhase(index, component, [SupportPhase.Active]);
    }

    /// <summary>
    /// Gets the latest version across all available products that matches the support phase.
    /// </summary>
    private static ReleaseVersion? GetLatestActiveVersion(IEnumerable<Product> index, InstallComponent component)
    {
        return GetLatestVersionBySupportPhase(index, component, [SupportPhase.Active]);
    }
    /// <summary>
    /// Gets the latest version across all available products that matches the support phase.
    /// </summary>
    private static ReleaseVersion? GetLatestVersionBySupportPhase(IEnumerable<Product> index, InstallComponent component, SupportPhase[] acceptedSupportPhases)
    {
        // A version in preview/ga/rtm support is considered Go Live and not Active.
        var activeSupportProducts = index?.Where(p => acceptedSupportPhases.Contains(p.SupportPhase));

        // The manifest is designed so that the first product will always be latest.
        Product? latestActiveSupportProduct = activeSupportProducts?.FirstOrDefault();

        return GetLatestReleaseVersionInProduct(latestActiveSupportProduct, component);
    }

    private static ReleaseVersion? GetLatestReleaseVersionInProduct(Product? product, InstallComponent component)
    {
        // Assumption: The latest runtime version will always be the same across runtime components.
        ReleaseVersion? latestVersion = component switch
        {
            InstallComponent.SDK => product?.LatestSdkVersion,
            _ => product?.LatestRuntimeVersion
        };

        return latestVersion;
    }

    /// <summary>
    ///  Replaces user input feature band strings into the full feature band.
    ///  This would convert '1xx' into '100'.
    ///  100 is not necessarily the latest but it is the feature band.
    ///  The other number in the band is the patch.
    /// </summary>
    /// <param name="band"></param>
    /// <returns></returns>
    private static int NormalizeFeatureBandInput(string band)
    {
        var bandString = band
            .Replace("X", "x", StringComparison.Ordinal)
            .Replace("x", "0", StringComparison.Ordinal)
            .PadRight(3, '0')
            .Substring(0, 3);
        return int.Parse(bandString, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the latest version for a feature band channel (e.g., "9.0.1xx").
    /// </summary>
    private static ReleaseVersion? GetLatestVersionForFeatureBand(ProductCollection index, int major, int minor, string featureBand, InstallComponent component)
    {
        if (component != InstallComponent.SDK)
        {
            return null;
        }

        var validProducts = GetProductsInMajorOrMajorMinor(index, major, minor);
        var latestProduct = validProducts.FirstOrDefault();
        var releases = latestProduct?.GetReleasesAsync().GetAwaiter().GetResult().ToList() ?? [];
        var normalizedFeatureBand = NormalizeFeatureBandInput(featureBand);

        foreach (var release in releases)
        {
            foreach (var sdk in release.Sdks)
            {
                if (sdk.Version.SdkFeatureBand == normalizedFeatureBand)
                {
                    return sdk.Version;
                }
            }
        }

        return null;
    }
}
