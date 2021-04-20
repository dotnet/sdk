// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    public class PackCheckResult
    {
        public PackCheckResult(IDownloadedPackInfo packInfo, PreFilterResultList preFilterResults)
        {
            PackInfo = packInfo;
            PreFilterResults = preFilterResults;
            FoundTemplates = new List<ITemplateInfo>();
        }

        public PackCheckResult(IDownloadedPackInfo packInfo, IReadOnlyList<ITemplateInfo> foundTemplates)
        {
            PackInfo = packInfo;
            PreFilterResults = new PreFilterResultList();
            FoundTemplates = foundTemplates;
        }

        public IDownloadedPackInfo PackInfo { get; }

        public PreFilterResultList PreFilterResults { get; }

        public IReadOnlyList<ITemplateInfo> FoundTemplates { get; }

        public bool AnyTemplates
        {
            get
            {
                return FoundTemplates.Count > 0;
            }
        }
    }
}
