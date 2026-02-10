// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
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
    /// Channel keyword for the latest Standard Term Support (STS) release.
    /// </summary>
    public const string StsChannel = "sts";

    /// <summary>
    /// Known channel keywords that are always valid.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownChannelKeywords = [LatestChannel, PreviewChannel, LtsChannel, StsChannel];

    /// <summary>
    /// Maximum reasonable major version number. .NET versions are currently single-digit;
    /// anything above 99 is clearly invalid input (e.g., typos, random numbers).
    /// </summary>
    internal const int MaxReasonableMajorVersion = 99;

    private ReleaseManifest _releaseManifest = new();

    public ChannelVersionResolver()
    {

    }

    public ChannelVersionResolver(ReleaseManifest releaseManifest)
    {
        _releaseManifest = releaseManifest;
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

        // Known keywords are always valid
        if (KnownChannelKeywords.Any(k => string.Equals(k, channel, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Try to parse as a version-like string
        var parts = channel.Split('.');
        if (parts.Length == 0 || parts.Length > 4)
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

        if (parts.Length >= 3)
        {
            var patch = parts[2];
            // Allow feature band pattern (e.g., "1xx", "100") or patch number
            var normalizedPatch = patch.Replace("x", "").Replace("X", "");
            if (normalizedPatch.Length > 0 && !int.TryParse(normalizedPatch, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses a version channel string into its components.
    /// </summary>
    /// <param name="channel">Channel string to parse (e.g., "9", "9.0", "9.0.1xx", "9.0.103")</param>
    /// <returns>Tuple containing (major, minor, featureBand, isFullySpecified)</returns>
    private (int Major, int Minor, string? FeatureBand, bool IsFullySpecified) ParseVersionChannel(UpdateChannel channel)
    {
        var parts = channel.Name.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : -1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : -1;

        // Check if we have a feature band (like 1xx) or a fully specified patch
        string? featureBand = null;
        bool isFullySpecified = false;

        if (parts.Length >= 3)
        {
            if (parts[2].EndsWith("xx"))
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
    /// <param name="channel">Channel string (e.g., "9", "9.0", "9.0.1xx", "9.0.103", "lts", "sts", "preview")</param>
    /// <param name="component">The component to check (ie SDK or runtime)</param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public ReleaseVersion? GetLatestVersionForChannel(UpdateChannel channel, InstallComponent component)
    {
        if (string.Equals(channel.Name, LtsChannel, StringComparison.OrdinalIgnoreCase) || string.Equals(channel.Name, StsChannel, StringComparison.OrdinalIgnoreCase))
        {
            var releaseType = string.Equals(channel.Name, LtsChannel, StringComparison.OrdinalIgnoreCase) ? ReleaseType.LTS : ReleaseType.STS;
            var productIndex = _releaseManifest.GetReleasesIndex();
            return GetLatestVersionByReleaseType(productIndex, releaseType, component);
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

    private IEnumerable<Product> GetProductsInMajorOrMajorMinor(IEnumerable<Product> index, int major, int? minor = null)
    {
        var validProducts = index.Where(p => minor is not null ? p.ProductVersion.Equals($"{major}.{minor}") : p.ProductVersion.StartsWith($"{major}."));
        return validProducts;
    }

    /// <summary>
    /// Gets the latest version for a major-only channel (e.g., "9").
    /// </summary>
    private ReleaseVersion? GetLatestVersionForMajorOrMajorMinor(IEnumerable<Product> index, int major, InstallComponent component, int? minor = null)
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
    private ReleaseVersion? GetLatestPreviewVersion(IEnumerable<Product> index, InstallComponent component)
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
            .Replace("X", "x")
            .Replace("x", "0")
            .PadRight(3, '0')
            .Substring(0, 3);
        return int.Parse(bandString);
    }


    /// <summary>
    /// Gets the latest version for a feature band channel (e.g., "9.0.1xx").
    /// </summary>
    private ReleaseVersion? GetLatestVersionForFeatureBand(ProductCollection index, int major, int minor, string featureBand, InstallComponent component)
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
