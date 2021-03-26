// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// Basic <see cref="ITemplatePackage"/> implementation so each
    /// <see cref="ITemplatePackageProvider"/> doesn't need to re-implement it.
    /// </summary>
    public class TemplatePackage : ITemplatePackage
    {
        public TemplatePackage(ITemplatePackageProvider provider, string mountPointUri, DateTime lastChangeTime)
        {
            Provider = provider;
            MountPointUri = mountPointUri;
            LastChangeTime = lastChangeTime;
        }

        /// <inheritdoc/>
        public ITemplatePackageProvider Provider { get; }

        /// <inheritdoc/>
        public string MountPointUri { get; }

        /// <inheritdoc/>
        public DateTime LastChangeTime { get; }
    }
}
