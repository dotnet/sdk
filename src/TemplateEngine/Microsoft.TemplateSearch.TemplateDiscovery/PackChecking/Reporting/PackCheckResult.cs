// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    internal class PackCheckResult
    {
        internal PackCheckResult(IDownloadedPackInfo packInfo, PreFilterResultList preFilterResults)
        {
            PackInfo = packInfo;
            PreFilterResults = preFilterResults;
            FoundTemplates = new List<ITemplateInfo>();
        }

        internal PackCheckResult(IDownloadedPackInfo packInfo, IReadOnlyList<ITemplateInfo> foundTemplates)
        {
            PackInfo = packInfo;
            PreFilterResults = new PreFilterResultList();
            FoundTemplates = foundTemplates;
        }

        internal IDownloadedPackInfo PackInfo { get; }

        internal PreFilterResultList PreFilterResults { get; }

        internal IReadOnlyList<ITemplateInfo> FoundTemplates { get; }

        internal bool AnyTemplates
        {
            get
            {
                return FoundTemplates.Count > 0;
            }
        }
    }
}
