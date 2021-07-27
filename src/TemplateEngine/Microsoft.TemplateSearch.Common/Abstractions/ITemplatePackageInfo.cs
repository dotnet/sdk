// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.Common.Abstractions
{
    /// <summary>
    /// Represents information about template package.
    /// </summary>
    public interface ITemplatePackageInfo
    {
        /// <summary>
        /// Gets template package name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets template package version.
        /// </summary>
        public string? Version { get; }

        /// <summary>
        /// Gets total number of downloads for the package.
        /// Optional, might be 0 in case search provider cannot provide number of downloads.
        /// </summary>
        public long TotalDownloads { get; }
    }
}
