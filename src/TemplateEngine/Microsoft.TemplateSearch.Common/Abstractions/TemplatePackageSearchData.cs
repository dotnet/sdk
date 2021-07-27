// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public partial class TemplatePackageSearchData : IPackageInfo
    {
        public TemplatePackageSearchData(IPackageInfo packInfo, IEnumerable<TemplateSearchData> templates, IDictionary<string, object>? data = null)
        {
            Name = packInfo.Name;
            Version = packInfo.Version;
            TotalDownloads = packInfo.TotalDownloads;
            Templates = templates.ToList();
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        public string Name { get; }

        public string? Version { get; }

        public long TotalDownloads { get; }

        public IReadOnlyList<TemplateSearchData> Templates { get; }

        public IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();
    }
}
