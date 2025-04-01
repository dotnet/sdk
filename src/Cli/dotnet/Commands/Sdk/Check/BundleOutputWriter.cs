// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Tools.Sdk.Check;

internal class BundleOutputWriter(
    ProductCollection productCollection,
    IProductCollectionProvider productCollectionProvider,
    IReporter reporter)
{
    protected ProductCollection _productCollection = productCollection;

    protected readonly IProductCollectionProvider _productCollectionProvider = productCollectionProvider;

    protected readonly IReporter _reporter = reporter;

    protected bool? BundleIsMaintenance(INetBundleInfo bundle)
    {
        return _productCollection
            .FirstOrDefault(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"))
            ?.SupportPhase.Equals(SupportPhase.Maintenance);
    }

    protected bool? BundleIsEndOfLife(INetBundleInfo bundle)
    {
        return _productCollection
            .FirstOrDefault(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"))
            ?.IsOutOfSupport();
    }
}
