// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public class MockProductCollectionProvider : IProductCollectionProvider
    {
        private readonly string _path;

        public MockProductCollectionProvider(string path)
        {
            _path = path;
        }

        public ProductCollection GetProductCollection(
            CancellationToken cancellationToken,
            Uri uri = null,
            string filePath = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ProductCollection.GetFromFileAsync(Path.Combine(_path, "releases-index.json"), false)
                .GetAwaiter()
                .GetResult();
        }

        public IEnumerable<ProductRelease> GetProductReleases(
            Product product,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return product.GetReleasesAsync(Path.Combine(_path, product.ProductVersion, "releases.json"), false)
                .GetAwaiter()
                .GetResult();
        }
    }
}
