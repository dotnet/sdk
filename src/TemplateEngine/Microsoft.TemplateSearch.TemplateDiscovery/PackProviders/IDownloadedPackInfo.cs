// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    internal interface IDownloadedPackInfo : IPackInfo
    {
        /// <summary>
        /// The fully qualified Id. Style may vary from source to source.
        /// </summary>
        string VersionedPackageIdentity { get; }

        /// <summary>
        /// The path on disk for the pack.
        /// </summary>
        string Path { get; }
    }
}
