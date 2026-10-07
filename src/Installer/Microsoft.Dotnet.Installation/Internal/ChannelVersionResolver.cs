// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
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
    /// Suffix that turns any partial version channel into a daily-build channel
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

    public ChannelVersionResolver(ReleaseManifest releaseManifest, DailyChannelResolver dailyChannelResolver)
    {
        _releaseManifest = releaseManifest;
        _dailyChannelResolver = dailyChannelResolver;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as instance for API symmetry with other resolver methods and to allow future stateful caching.")]
    public IEnumerable<string> GetSupportedChannels(bool includeFeatureBands = true)
    {
        var productIndex = ReleaseManifest.Default.GetReleasesIndex();
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
                ..ReleaseManifest.Default.GetReleases(product)
                    .SelectMany(r => r.Sdks)
                    .Select(sdk => sdk.Version)
                    .OrderByDescending(v => v)
                    .Select(v => $"{v.Major}.{v.Minor}.{(v.Patch / 100)}xx")
                    .Distinct()
                    .ToList()
                ];
        }

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

        // Known keywords are always valid
        if (KnownChannelKeywords.Any(k => string.Equals(k, channel, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // The only two forms that include a '-' are:
        //   * "<partial-version>-daily" (e.g. "10.0-daily", "10.0.1xx-daily"),
        //     optionally with a prerelease-label qualifier ("11.0.1xx-preview.5-daily"
        //     or "11.0.1xx-preview5-daily"). Daily only applies to scopes; a
        //     specific patch like "10.0.103-daily" is already specific and is
        //     rejected.
        //   * a fully-qualified version with a prerelease tag (e.g. "10.0.100-preview.1.32640").
        //     The prerelease tag is opaque; we only validate the numeric prefix.
        if (channel.EndsWith(DailySuffix, StringComparison.OrdinalIgnoreCase))
        {
            var basePart = channel.Substring(0, channel.Length - DailySuffix.Length);
            if (string.IsNullOrEmpty(basePart))
            {
                return false;
            }

            if (UpdateChannel.TrySplitPartialVersionAndPrereleaseLabel(basePart, out var bandPart, out _))
            {
                return IsValidPartialVersion(bandPart);
            }

            return IsValidPartialVersion(basePart);
        }

        var dashIndex = channel.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            var versionPart = channel.Substring(0, dashIndex);
            return IsValidNumericVersion(versionPart);
        }

        return IsValidPartialVersion(channel) || IsValidNumericVersion(channel);
    }

    /// <summary>
    /// Validates a partial version channel string: bare major (e.g. <c>10</c>),
    /// major.minor (e.g. <c>10.0</c>), or feature band (e.g. <c>10.0.1xx</c>).
    /// Rejects fully-qualified versions and prerelease tags.
    /// </summary>
    private static bool IsValidPartialVersion(string partialVersion)
    {
        var parts = partialVersion.Split('.');
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
            // A partial version's third part must be a feature band pattern like "1xx", not a numeric patch.
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
    /// Validates a numeric <c>major.minor.patch</c> version (no feature band, no
    /// prerelease tag, all three parts required).
    /// </summary>
    private static bool IsValidNumericVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) || major < 0 || major > MaxReasonableMajorVersion)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var minor) || minor < 0)
        {
            return false;
        }

        if (!int.TryParse(parts[2], out var patch) || patch < 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the latest fully specified version for a given channel string (major, major.minor, or feature band).
    /// </summary>
    /// <param name="channel">Channel string (e.g., "9", "9.0", "9.0.1xx", "9.0.103", "lts", "preview", "10.0.1xx-daily")</param>
    /// <param name="component">The component to check (ie SDK or runtime)</param>
    /// <param name="architecture">
    /// Architecture to use when resolving daily channels (selects the correct aka.ms RID-suffixed
    /// link). Optional; defaults to the current process architecture. Ignored for non-daily channels.
    /// </param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public ReleaseVersion? GetLatestVersionForChannel(UpdateChannel channel, InstallComponent component, InstallArchitecture? architecture = null)
    {
        // Daily channels are resolved via aka.ms redirect rather than the release manifest.
        if (channel.IsDaily)
        {
            _dailyChannelResolver ??= new DailyChannelResolver(_releaseManifest);
            var dailyVersion = _dailyChannelResolver.Resolve(
                channel,
                architecture ?? InstallerUtilities.GetDefaultInstallArchitecture(),
                component);
            return dailyVersion is not null && channel.Matches(dailyVersion) ? dailyVersion : null;
        }

        if (ReleaseVersion.TryParse(channel.Name, out var exact))
        {
            return channel.Matches(exact) ? exact : null;
        }

        if (!IsValidChannelFormat(channel.Name)
            || (component != InstallComponent.SDK && channel.IsSdkVersionOrFeatureBand()))
        {
            return null;
        }

        var manifest = ReleaseManifest.Default;
        return GetLatestMatchingVersion(channel, component, manifest.GetReleasesIndex(), manifest.GetReleases);
    }

    /// <summary>
    /// Selects the latest matching component version from eligible release products.
    /// Loads eligible products newest-first, stopping when a matching version is found.
    /// </summary>
    internal static ReleaseVersion? GetLatestMatchingVersion(
        UpdateChannel channel,
        InstallComponent component,
        IEnumerable<Product> index,
        Func<Product, IEnumerable<ProductRelease>> getReleases)
    {
        var parts = channel.Name.Split('.');
        var products = index.Where(product =>
        {
            var productVersion = new Version(product.ProductVersion);
            return (channel.MinimumVersion is null || productVersion.Major >= channel.MinimumVersion.Major)
                && (!int.TryParse(parts[0], out var major) || productVersion.Major == major)
                && (parts.Length < 2 || !int.TryParse(parts[1], out var minor) || productVersion.Minor == minor);
        });
        if (channel.Name.Equals(LtsChannel, StringComparison.OrdinalIgnoreCase))
        {
            products = products.Where(p => p.ReleaseType == ReleaseType.LTS);
        }

        // Component versions follow the product's major/minor, so older products
        // cannot provide a greater match once this product satisfies the request.
        foreach (var product in products.OrderByDescending(p => new Version(p.ProductVersion)))
        {
            var releases = getReleases(product);
            var versions = component == InstallComponent.SDK
                ? releases.SelectMany(r => r.Sdks).Select(s => s.Version)
                : releases.SelectMany(r => r.Runtimes)
                    .Where(r => ReleaseManifest.IsMatchingRuntimeComponent(r.Name, component)).Select(r => r.Version);
            var latest = versions.Where(channel.Matches).OrderByDescending(v => v).FirstOrDefault();
            if (latest is not null)
            {
                return latest;
            }
        }

        return null;
    }
}
