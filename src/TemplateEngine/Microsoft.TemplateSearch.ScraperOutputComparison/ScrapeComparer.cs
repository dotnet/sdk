// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateSearch.ScraperOutputComparison
{
    internal class ScrapeComparer
    {
        public ScrapeComparer(ComparisonConfig config)
        {
            _config = config;
            ITemplateEngineHost host = TemplateEngineHostHelper.CreateHost("Comparison");
            _environmentSettings = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
        }

        private readonly ComparisonConfig _config;
        private readonly EngineEnvironmentSettings _environmentSettings;

        // For now, it's just going to check the packs between the two runs
        // As desired, add more comparisons, and expand the definition of ScrapeComparisonResult
        public bool Compare(out ScrapeComparisonResult result)
        {
            if (!TryReadScraperOutput(_config.ScraperOutputOneFile, out TemplateDiscoveryMetadata scraperOutputOne)
                || ! TryReadScraperOutput(_config.ScraperOutputTwoFile, out TemplateDiscoveryMetadata scraperOutputTwo))
            {
                result = null;
                return false;
            }

            // In one but not two
            HashSet<string> scraperOnePacks = new HashSet<string>(scraperOutputOne.PackToTemplateMap.Keys);
            scraperOnePacks.ExceptWith(scraperOutputTwo.PackToTemplateMap.Keys);

            // In two but not one
            HashSet<string> scraperTwoPacks = new HashSet<string>(scraperOutputTwo.PackToTemplateMap.Keys);
            scraperTwoPacks.ExceptWith(scraperOutputOne.PackToTemplateMap.Keys);

            result = new ScrapeComparisonResult(_config.ScraperOutputOneFile, _config.ScraperOutputTwoFile, scraperOnePacks.ToList(), scraperTwoPacks.ToList());
            return true;
        }

        private bool TryReadScraperOutput(string scrapeFilePath, out TemplateDiscoveryMetadata discoveryMetadata)
        {
            ISearchCacheConfig cacheConfig = new NuGetSearchCacheConfig(scrapeFilePath);
            return FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(_environmentSettings, cacheConfig, out discoveryMetadata);
        }
    }
}
