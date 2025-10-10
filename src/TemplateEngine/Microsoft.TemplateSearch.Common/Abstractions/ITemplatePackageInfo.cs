// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

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
        string Name { get; }

        /// <summary>
        /// Gets template package version.
        /// </summary>
        string? Version { get; }

        /// <summary>
        /// Gets total number of downloads for the package.
        /// Optional, might be 0 in case search provider cannot provide number of downloads.
        /// </summary>
        long TotalDownloads { get; }

        /// <summary>
        /// Gets the list of template package owners.
        /// </summary>
        IReadOnlyList<string> Owners { get; }

        /// <summary>
        /// Gets the indication if the package is verified.
        /// </summary>
        /// <remarks>
        /// For NuGet.org 'verified' means that package ID is under reserved namespaces, see  <see href="https://docs.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation"/>.
        /// </remarks>
        bool Reserved { get; }

        /// <summary>
        /// Gets the NuGet package description.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets the URL to the package icon.
        /// </summary>
        string? IconUrl { get; }
    }
}
