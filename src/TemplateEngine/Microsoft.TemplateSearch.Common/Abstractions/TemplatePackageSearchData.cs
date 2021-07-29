// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    /// <summary>
    /// Template package searchable data.
    /// </summary>
    public partial class TemplatePackageSearchData : ITemplatePackageInfo
    {
        public TemplatePackageSearchData(ITemplatePackageInfo packInfo, IEnumerable<TemplateSearchData> templates, IDictionary<string, object>? data = null)
        {
            if (packInfo is null)
            {
                throw new ArgumentNullException(nameof(packInfo));
            }

            if (templates is null)
            {
                throw new ArgumentNullException(nameof(templates));
            }

            Name = !string.IsNullOrWhiteSpace(packInfo.Name) ? packInfo.Name
                : throw new ArgumentException($"{nameof(packInfo.Name)} should not be null or empty", nameof(packInfo));
            Version = packInfo.Version;
            TotalDownloads = packInfo.TotalDownloads;
            Owners = packInfo.Owners;
            Verified = packInfo.Verified;
            Templates = templates.ToList();
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string? Version { get; }

        /// <inheritdoc/>
        public long TotalDownloads { get; }

        /// <inheritdoc/>
        public IReadOnlyList<string> Owners { get; }

        /// <inheritdoc/>
        public bool Verified { get; }

        /// <summary>
        /// Gets the list of templates in template package.
        /// </summary>
        public IReadOnlyList<TemplateSearchData> Templates { get; }

        /// <summary>
        /// Gets the additional data available for template package.
        /// </summary>
        /// <remarks>
        /// Additional data may be read by additional readers provider to <see cref="ITemplateSearchProviderFactory"/> when creating the <see cref="ITemplateSearchProvider"/>.
        /// </remarks>
        public IDictionary<string, object> AdditionalData { get; }
    }
}
