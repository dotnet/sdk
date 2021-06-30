// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    internal static class NugetPackScraper
    {
        private static readonly Dictionary<string, string> SupportedProviders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "query-package-type-template", "packageType=Template" },
            { "query-template", "q=template" }
        };

        internal static IEnumerable<string> SupportedProvidersList => SupportedProviders.Keys;

        internal static bool TryCreateDefaultNugetPackScraper(ScraperConfig config, out PackSourceChecker? packSourceChecker)
        {
            if (string.IsNullOrWhiteSpace(config.BasePath))
            {
                throw new ArgumentException($"{nameof(config.BasePath)} should not be null or whitespace.");
            }
            List<IPackProvider> providers = new List<IPackProvider>();

            if (!string.IsNullOrWhiteSpace(config.LocalPackagePath))
            {
                providers.Add(new TestPackProvider(config.LocalPackagePath));
            }
            else if (!config.Providers.Any())
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

            if (!string.IsNullOrWhiteSpace(config.PreviousRunBasePath))
            {
                if (!PreviouslyRejectedPackFilter.TryGetPreviouslySkippedPacks(config.PreviousRunBasePath, out HashSet<string>? nonTemplatePacks) || nonTemplatePacks == null)
                {
                    Console.WriteLine("Unable to read results from the previous run.");
                    packSourceChecker = null;
                    return false;
                }
                preFilterList.Add(PreviouslyRejectedPackFilter.SetupPackFilter(nonTemplatePacks));
            }

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
