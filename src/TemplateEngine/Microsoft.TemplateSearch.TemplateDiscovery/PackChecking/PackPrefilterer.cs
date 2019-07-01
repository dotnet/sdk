using System;
using System.Collections.Generic;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    public class PackPreFilterer
    {
        private readonly IReadOnlyList<Func<IInstalledPackInfo, PreFilterResult>> _preFilters;

        public PackPreFilterer(IReadOnlyList<Func<IInstalledPackInfo, PreFilterResult>> preFilters)
        {
            _preFilters = preFilters;
        }

        public PreFilterResultList FilterPack(IInstalledPackInfo packInfo)
        {
            List<PreFilterResult> resultList = new List<PreFilterResult>();

            foreach (Func<IInstalledPackInfo, PreFilterResult> filter in _preFilters)
            {
                PreFilterResult result = filter(packInfo);
                resultList.Add(result);
            }

            return new PreFilterResultList(resultList);
        }
    }
}
