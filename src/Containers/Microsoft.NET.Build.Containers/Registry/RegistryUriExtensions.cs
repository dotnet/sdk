// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal static class RegistryUriExtensions
{
    internal static bool IsAmazonECRRegistry(this Uri uri)
    {
        if (uri.Authority.Contains(RegistryConstants.PublicAmazonElasticContainerRegistryDomain))
        {
            return true;
        }

        string accountId = uri.Authority.Split('.')[0];
        return (uri.Authority.Contains(".ecr.") || uri.Authority.Contains(".ecr-"))
            && accountId.Length == 12
            && long.TryParse(accountId, out _);
    }
}
