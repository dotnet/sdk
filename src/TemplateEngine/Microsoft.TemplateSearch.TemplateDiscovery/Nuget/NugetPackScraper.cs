using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.Filters;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackScraper
    {
        static readonly Dictionary<string, string> SupportedProviders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {  "query-package-type-template", "packageType=Template" },
            {  "query-template", "q=template" }
        };

        public static IEnumerable<string> SupportedProvidersList => SupportedProviders.Keys;

        public static bool TryCreateDefaultNugetPackScraper(ScraperConfig config, out PackSourceChecker packSourceChecker)
        {
            List<IPackProvider> providers = new List<IPackProvider>();

            if (!config.Providers.Any())
            {
                providers.AddRange(SupportedProviders.Select(kvp => new NugetPackProvider(kvp.Key, kvp.Value, config.BasePath, config.PageSize, config.RunOnlyOnePage, config.IncludePreviewPacks)));
            }
            else
            {
                foreach (string provider in config.Providers.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    providers.Add(new NugetPackProvider(provider, SupportedProviders[provider], config.BasePath, config.PageSize, config.RunOnlyOnePage, config.IncludePreviewPacks));
                }
            }

            List<Func<IDownloadedPackInfo, PreFilterResult>> preFilterList = new List<Func<IDownloadedPackInfo, PreFilterResult>>();

            if (!PreviouslyRejectedPackFilter.TryGetPreviouslySkippedPacks(config.PreviousRunBasePath, out HashSet<string> nonTemplatePacks))
            {
                Console.WriteLine("Unable to read results from the previous run.");
                packSourceChecker = null;
                return false;
            }

            preFilterList.Add(PreviouslyRejectedPackFilter.SetupPackFilter(nonTemplatePacks));
            if (!config.DontFilterOnTemplateJson)
            {
                preFilterList.Add(TemplateJsonExistencePackFilter.SetupPackFilter());
            }
            preFilterList.Add(SkipTemplatePacksFilter.SetupPackFilter());

            PackPreFilterer preFilterer = new PackPreFilterer(preFilterList);

            IReadOnlyList<IAdditionalDataProducer> additionalDataProducers = new List<IAdditionalDataProducer>()
            {
                new CliHostDataProducer()
            };

            packSourceChecker = new PackSourceChecker(providers, preFilterer, additionalDataProducers, config.SaveCandidatePacks);
            return true;
        }
    }
}
