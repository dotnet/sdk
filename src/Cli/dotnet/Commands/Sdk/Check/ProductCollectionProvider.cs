// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

public class ProductCollectionProvider : IProductCollectionProvider
{
    public ProductCollection GetProductCollection(Uri uri = null, string filePath = null)
    {
        try
        {
            return uri != null ? Task.Run(() => ProductCollection.GetAsync(uri.ToString())).Result :
                filePath != null ? Task.Run(() => ProductCollection.GetFromFileAsync(filePath, false)).Result :
                Task.Run(() => ProductCollection.GetAsync()).Result;
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
            return product.GetReleasesAsync().Result;
        }
        catch (Exception ex) when (ex is HttpRequestException ||
                                     ex is FileNotFoundException ||
                                     ex is AggregateException aggregateEx && 
                                     (aggregateEx.InnerException is HttpRequestException || 
                                      aggregateEx.InnerException is FileNotFoundException))
        {
            // Return empty collection if releases are not available (e.g., unreleased preview versions)
            // This handles cases where the releases.json file doesn't exist yet for a version
            return Enumerable.Empty<ProductRelease>();
        }
    }
}
