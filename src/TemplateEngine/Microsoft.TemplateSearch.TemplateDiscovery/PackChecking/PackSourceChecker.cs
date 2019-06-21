using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    public class PackSourceChecker
    {
        private readonly IPackProvider _packProvider;
        private readonly PackPreFilterer _packPreFilterer;
        private readonly PackChecker _packChecker;
        private readonly IReadOnlyList<IAdditionalDataProducer> _additionalDataProducers;
        private readonly bool _saveCandidatePacks;

        public PackSourceChecker(IPackProvider packProvider, PackPreFilterer packPreFilterer, IReadOnlyList<IAdditionalDataProducer> additionalDataProducers, bool saveCandidatePacks)
        {
            _packProvider = packProvider;
            _packPreFilterer = packPreFilterer;
            _additionalDataProducers = additionalDataProducers;
            _saveCandidatePacks = saveCandidatePacks;

            _packChecker = new PackChecker();
        }

        public PackSourceCheckResult CheckPackages()
        {
            List<PackCheckResult> checkResultList = new List<PackCheckResult>();
            HashSet<string> alreadySeenTemplateIdentities = new HashSet<string>();

            int count = 0;

            foreach (IInstalledPackInfo packInfo in _packProvider.CandidatePacks)
            {
                PackCheckResult preFilterResult = PrefilterPackInfo(packInfo);
                if (preFilterResult.PreFilterResults.ShouldBeFiltered)
                {
                    checkResultList.Add(preFilterResult);
                }
                else
                {
                    PackCheckResult packCheckResult = _packChecker.TryGetTemplatesInPack(packInfo, _additionalDataProducers, alreadySeenTemplateIdentities);
                    checkResultList.Add(packCheckResult);

                    // Record the found identities - to skip these templates when checking additional packs.
                    // Some template identities are the same in multiple packs on nuget.
                    // For this scraper, first in wins.
                    alreadySeenTemplateIdentities.UnionWith(packCheckResult.FoundTemplates.Select(t => t.Identity));
                }

                ++count;
                if ((count % 10) == 0)
                {
                    Console.WriteLine($"{count} packs processed");
                }
            }

            if (!_saveCandidatePacks)
            {
                _packProvider.DeleteDownloadedPacks();
            }

            return new PackSourceCheckResult(checkResultList, _additionalDataProducers);
        }

        private PackCheckResult PrefilterPackInfo(IInstalledPackInfo packInfo)
        {
            PreFilterResultList preFilterResult = _packPreFilterer.FilterPack(packInfo);
            return new PackCheckResult(packInfo, preFilterResult);
        }
    }
}
