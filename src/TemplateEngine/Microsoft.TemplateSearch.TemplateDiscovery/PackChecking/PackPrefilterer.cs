// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal class PackPreFilterer
    {
        private readonly IReadOnlyList<Func<IDownloadedPackInfo, PreFilterResult>> _preFilters;

        internal PackPreFilterer(IReadOnlyList<Func<IDownloadedPackInfo, PreFilterResult>> preFilters)
        {
            _preFilters = preFilters;
        }

        internal PreFilterResultList FilterPack(IDownloadedPackInfo packInfo)
        {
            List<PreFilterResult> resultList = new List<PreFilterResult>();

            foreach (Func<IDownloadedPackInfo, PreFilterResult> filter in _preFilters)
            {
                PreFilterResult result = filter(packInfo);
                resultList.Add(result);
            }

            return new PreFilterResultList(resultList);
        }
    }
}
