// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;


namespace Microsoft.Dotnet.Installation.Internal;

internal class ChannelVersionResolver
{
    private ReleaseManifest _releaseManifest = new();

    public ChannelVersionResolver()
    {

    }

    public ChannelVersionResolver(ReleaseManifest releaseManifest)
    {
        _releaseManifest = releaseManifest;
    }

    public ReleaseVersion? Resolve(DotnetInstallRequest installRequest)
    {
        return GetLatestVersionForChannel(installRequest.Channel, installRequest.Component);
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
    /// <param name="mode">InstallMode.SDK or InstallMode.Runtime</param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public ReleaseVersion? GetLatestVersionForChannel(UpdateChannel channel, InstallComponent component)
    {
        if (string.Equals(channel.Name, "lts", StringComparison.OrdinalIgnoreCase) || string.Equals(channel.Name, "sts", StringComparison.OrdinalIgnoreCase))
        {
            var releaseType = string.Equals(channel.Name, "lts", StringComparison.OrdinalIgnoreCase) ? ReleaseType.LTS : ReleaseType.STS;
            var productIndex = _releaseManifest.GetReleasesIndex();
            return GetLatestVersionByReleaseType(productIndex, releaseType, component);
        }
        else if (string.Equals(channel.Name, "preview", StringComparison.OrdinalIgnoreCase))
        {
            var productIndex = _releaseManifest.GetReleasesIndex();
            return GetLatestPreviewVersion(productIndex, component);
        }
        else if (string.Equals(channel.Name, "latest", StringComparison.OrdinalIgnoreCase))
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
        var validProducts = index.Where(p => p.ProductVersion.StartsWith(minor is not null ? $"{major}.{minor}" : $"{major}."));
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
    /// <param name="isLts">True for LTS (Long-Term Support), false for STS (Standard-Term Support)</param>
    /// <param name="mode">InstallComponent.SDK or InstallComponent.Runtime</param>
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
    /// <param name="mode">InstallComponent.SDK or InstallComponent.Runtime</param>
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
