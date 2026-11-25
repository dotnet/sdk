// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

public class ProductCollectionProvider : IProductCollectionProvider
{
    public ProductCollection GetProductCollection(
        CancellationToken cancellationToken,
        Uri uri = null,
        string filePath = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<ProductCollection> productCollection = uri != null
                ? ProductCollection.GetAsync(uri.ToString())
                : filePath != null
                    ? ProductCollection.GetFromFileAsync(filePath, false)
                    : ProductCollection.GetAsync();

            return productCollection.WaitAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new GracefulException(string.Format(CliCommandStrings.ReleasesLibraryFailed, e.Message));
        }
    }

    public IEnumerable<ProductRelease> GetProductReleases(
        Deployment.DotNet.Releases.Product product,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return product.GetReleasesAsync()
                .WaitAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is FileNotFoundException)
        {
            // Return empty collection if releases are not available (e.g., unreleased preview versions)
            // This handles cases where the releases.json file doesn't exist yet for a version
            return Enumerable.Empty<ProductRelease>();
        }
    }
}
