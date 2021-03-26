// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// Represents the template package.
    /// Template package is a folder, .nupkg or other container that can contain single or multiple templates.
    /// <seealso cref="ITemplatePackageProvider"/> for more information.
    /// </summary>
    public interface ITemplatePackage
    {
        /// <summary>
        /// Gets the last changed time for the template package.
        /// To avoid scanning for changes every time, template engine is caching templates from
        /// template packages, this timestamp is used to invalidate content and re-scan this template package.
        /// </summary>
        DateTime LastChangeTime { get; }

        /// <summary>
        /// Gets mount point URI - unique location of template package.
        /// This can be full URI like file://, http:// or simply file path.
        /// </summary>
        /// <remarks>
        /// Supported mount points are defined in <see cref="IMountPoint"/> implementations.
        /// </remarks>
        string MountPointUri { get; }

        /// <summary>
        /// Gets the <see cref="ITemplatePackageProvider"/> that created the template package.
        /// This is mostly helper for grouping packages by provider
        /// so caller doesn't need to keep track of provider->package relation.
        /// </summary>
        ITemplatePackageProvider Provider { get; }
    }
}
