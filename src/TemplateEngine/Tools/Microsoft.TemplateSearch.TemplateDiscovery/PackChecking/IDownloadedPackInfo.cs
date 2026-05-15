// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal interface IDownloadedPackInfo : ITemplatePackageInfo
    {
        /// <summary>
        /// The path on disk for the pack.
        /// </summary>
        string Path { get; }
    }
}
