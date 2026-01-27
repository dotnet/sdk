// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

public class ProductCollectionProvider : IProductCollectionProvider
{
    private const string ReleasesCacheFolderName = "releases";

    private static string GetReleasesIndexCachePath()
    {
        return Path.Combine(
            CliFolderPathCalculator.DotnetUserProfileFolderPath,
            ReleasesCacheFolderName,
            "releases-index.json");
    }

    private static string GetProductReleasesCachePath(string productVersion)
    {
        return Path.Combine(
            CliFolderPathCalculator.DotnetUserProfileFolderPath,
            ReleasesCacheFolderName,
            productVersion,
            "releases.json");
    }

    public ProductCollection GetProductCollection(Uri uri = null, string filePath = null)
    {
        try
        {
            if (uri != null)
            {
                return Task.Run(() => ProductCollection.GetAsync(uri.ToString())).Result;
            }
            
            if (filePath != null)
            {
                return Task.Run(() => ProductCollection.GetFromFileAsync(filePath, false)).Result;
            }

            // Use caching with proper path under .dotnet/releases
            string cachePath = GetReleasesIndexCachePath();
            return Task.Run(() => ProductCollection.GetFromFileAsync(cachePath, downloadLatest: true)).Result;
        }
        catch (Exception e)
        {
            throw new GracefulException(string.Format(CliCommandStrings.ReleasesLibraryFailed, e.Message));
        }
    }

    public IEnumerable<ProductRelease> GetProductReleases(Deployment.DotNet.Releases.Product product)
    {
        try
        {
            // Use caching with proper path under .dotnet/releases/{version}
            string cachePath = GetProductReleasesCachePath(product.ProductVersion);
            return product.GetReleasesAsync(cachePath, downloadLatest: true).Result;
        }
        catch (Exception e)
        {
            throw new GracefulException(string.Format(CliCommandStrings.ReleasesLibraryFailed, e.Message));
        }
    }
}
