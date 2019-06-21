using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Microsoft.TemplateSearch.TemplateDiscovery.Results;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackScraper
    {
        private static readonly string PreviouslySeenPrefilterId = "Previously Seen";

        public static bool TryCreateDefaultNugetPackScraper(ScraperConfig config, out PackSourceChecker packSourceChecker)
        {
            NugetPackProvider packProvider = new NugetPackProvider(config.BasePath, config.PageSize, config.RunOnlyOnePage, config.IncludePreviewPacks);

            List<Func<IInstalledPackInfo, PreFilterResult>> preFilterList = new List<Func<IInstalledPackInfo, PreFilterResult>>();

            if (!TryGetPreviouslySkippedPacks(config.PreviousRunBasePath, out HashSet<string> nonTemplatePacks))
            {
                Console.WriteLine("Unable to read results from the previous run.");
                packSourceChecker = null;
                return false;
            }
            else
            {
                preFilterList.Add(SetupPreviouslyRejectedPackFilter(nonTemplatePacks));
            }

            if (!config.DontFilterOnTemplateJson)
            {
                preFilterList.Add(TemplateJsonExistencePackFilter.SetupPackFilter());
            }

            PackPreFilterer preFilterer = new PackPreFilterer(preFilterList);

            IReadOnlyList<IAdditionalDataProducer> additionalDataProducers = new List<IAdditionalDataProducer>()
            {
                new CliHostDataProducer()
            };

            packSourceChecker = new PackSourceChecker(packProvider, preFilterer, additionalDataProducers, config.SaveCandidatePacks);
            return true;
        }

        private static bool TryGetPreviouslySkippedPacks(string previousRunBasePath, out HashSet<string> nonTemplatePacks)
        {
            if (string.IsNullOrEmpty(previousRunBasePath))
            {
                nonTemplatePacks = new HashSet<string>();
                return true;
            }
            else if (Directory.Exists(previousRunBasePath))
            {
                string nonTemplatePackDataFile = Path.Combine(previousRunBasePath, PackCheckResultReportWriter.CacheContentDirectory, PackCheckResultReportWriter.NonTemplatePacksFileName);
                string fileContents = File.ReadAllText(nonTemplatePackDataFile);
                nonTemplatePacks = JsonConvert.DeserializeObject<HashSet<string>>(fileContents);
                return true;
            }

            nonTemplatePacks = null;
            return false;
        }

        private static Func<IInstalledPackInfo, PreFilterResult> SetupPreviouslyRejectedPackFilter(HashSet<string> nonTemplatePacks)
        {
            Func<IInstalledPackInfo, PreFilterResult> previouslyRejectedPackFilter = (packInfo) =>
            {
                if (nonTemplatePacks.Contains(packInfo.Id))
                {
                    return new PreFilterResult()
                    {
                        FilterId = PreviouslySeenPrefilterId,
                        IsFiltered = true,
                        Reason = "Package was previously examined, and does not contain templates."
                    };
                }

                return new PreFilterResult()
                {
                    FilterId = PreviouslySeenPrefilterId,
                    IsFiltered = false,
                    Reason = string.Empty
                };
            };

            return previouslyRejectedPackFilter;
        }
    }
}
