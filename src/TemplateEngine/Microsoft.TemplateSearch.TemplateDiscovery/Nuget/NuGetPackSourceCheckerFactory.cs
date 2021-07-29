// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.Filters;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.NuGet
{
    internal static class NuGetPackSourceCheckerFactory
    {
        private static readonly Dictionary<SupportedQueries, string> SupportedProviders = new Dictionary<SupportedQueries, string>()
        {
            { SupportedQueries.PackageTypeQuery, "packageType=Template" },
            { SupportedQueries.TemplateQuery, "q=template" }
        };

        internal static PackSourceChecker CreatePackSourceChecker(CommandArgs config)
        {
            List<IPackProvider> providers = new List<IPackProvider>();

            if (config.LocalPackagePath != null)
            {
                providers.Add(new TestPackProvider(config.LocalPackagePath));
            }
            else if (!config.Queries.Any())
            {
                providers.AddRange(SupportedProviders.Select(kvp => new NuGetPackProvider(kvp.Key.ToString(), kvp.Value, config.OutputPath, config.PageSize, config.RunOnlyOnePage, config.IncludePreviewPacks)));
            }
            else
            {
                foreach (SupportedQueries provider in config.Queries.Distinct())
                {
                    providers.Add(new NuGetPackProvider(provider.ToString(), SupportedProviders[provider], config.OutputPath, config.PageSize, config.RunOnlyOnePage, config.IncludePreviewPacks));
                }
            }

            List<Func<IDownloadedPackInfo, PreFilterResult>> preFilterList = new List<Func<IDownloadedPackInfo, PreFilterResult>>();

            if (!config.DontFilterOnTemplateJson)
            {
                preFilterList.Add(TemplateJsonExistencePackFilter.SetupPackFilter());
            }
            preFilterList.Add(SkipTemplatePacksFilter.SetupPackFilter());
            preFilterList.Add(FilterNonMicrosoftAuthors.SetupPackFilter());

            PackPreFilterer preFilterer = new PackPreFilterer(preFilterList);

            IReadOnlyList<IAdditionalDataProducer> additionalDataProducers = new List<IAdditionalDataProducer>()
            {
                new CliHostDataProducer()
            };

            return new PackSourceChecker(providers, preFilterer, additionalDataProducers, config.SaveCandidatePacks);
        }
    }
}
