// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

internal class SdkOutputWriter(
    IEnumerable<NetSdkInfo> sdkInfo,
    ProductCollection productCollection,
    IProductCollectionProvider productCollectionProvider,
    IReporter reporter) : BundleOutputWriter(productCollection, productCollectionProvider, reporter)
{
    private readonly IEnumerable<NetSdkInfo> _sdkInfo = sdkInfo;

    public void PrintSdkInfo()
    {
        _reporter.WriteLine(CliCommandStrings.SdkSectionHeader);

        var table = new PrintableTable<NetSdkInfo>();
        table.AddColumn(CliCommandStrings.VersionColumnHeader, sdk => sdk.Version.ToString());
        table.AddColumn(CliCommandStrings.StatusColumnHeader, sdk => GetSdkStatusMessage(sdk));

        table.PrintRows(_sdkInfo.OrderBy(sdk => sdk.Version), l => _reporter.WriteLine(l));

        if (NewFeatureBandAvailable())
        {
            _reporter.WriteLine();
            // advertise newest feature band
            _reporter.WriteLine(string.Format(CliCommandStrings.NewFeatureBandMessage, NewestFeatureBandAvailable()));
        }
    }

    private string GetSdkStatusMessage(NetSdkInfo sdk)
    {
        bool? isEndOfLife = BundleIsEndOfLife(sdk);
        bool? isMaintenance = BundleIsMaintenance(sdk);
        bool sdkPatchExists = NewerSdkPatchExists(sdk);
        if (isEndOfLife == true)
        {
            return string.Format(CliCommandStrings.OutOfSupportMessage, $"{sdk.Version.Major}.{sdk.Version.Minor}");
        }
        else if (isMaintenance == true)
        {
            return string.Format(CliCommandStrings.MaintenanceMessage, $"{sdk.Version.Major}.{sdk.Version.Minor}");
        }
        else if (sdkPatchExists)
        {
            return string.Format(CliCommandStrings.NewPatchAvailableMessage, NewestSdkPatchVersion(sdk));
        }
        else if (isEndOfLife == false && isMaintenance == false && !sdkPatchExists)
        {
            return CliCommandStrings.BundleUpToDateMessage;
        }
        else
        {
            return CliCommandStrings.VersionCheckFailure;
        }
    }

    private bool NewerSdkPatchExists(NetSdkInfo bundle)
    {
        var newestPatchVesion = NewestSdkPatchVersion(bundle);
        return newestPatchVesion == null ? false : newestPatchVesion > bundle.Version;
    }

    private ReleaseVersion? NewestSdkPatchVersion(NetSdkInfo bundle)
    {
        var product = _productCollection.First(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"));
        if (product.LatestSdkVersion.SdkFeatureBand == bundle.Version.SdkFeatureBand)
        {
            // This is the latest feature band
            return product.LatestSdkVersion;
        }
        else
        {
            // Fetch detailed product release information
            var productReleases = _productCollectionProvider.GetProductReleases(product);
            var featureBandVersions = productReleases
                .SelectMany(release => release.Sdks)
                .Select(sdk => sdk.Version)
                .Where(sdkVersion => sdkVersion.SdkFeatureBand == bundle.Version.SdkFeatureBand);
            return featureBandVersions.FirstOrDefault();
        }
    }

    private bool NewFeatureBandAvailable()
    {
        return NewestFeatureBandAvailable() > _sdkInfo.Select(sdk => sdk.Version).Max();
    }

    private ReleaseVersion NewestFeatureBandAvailable()
    {
        return _productCollection.OrderByDescending(product => product.ProductVersion).First().LatestSdkVersion;
    }
}
