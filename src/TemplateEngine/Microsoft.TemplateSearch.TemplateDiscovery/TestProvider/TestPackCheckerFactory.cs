// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.Filters;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal class TestPackCheckerFactory : IPackCheckerFactory
    {
        public Task<PackSourceChecker> CreatePackSourceCheckerAsync(CommandArgs config, CancellationToken cancellationToken)
        {
            List<IPackProvider> providers = new List<IPackProvider>() { new TestPackProvider(config.LocalPackagePath ?? throw new ArgumentNullException(nameof(config.LocalPackagePath))) };

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

            TemplateSearchCache? existingCache = LoadExistingCache(config);
            IEnumerable<FilteredPackageInfo>? knownNonTemplatePackages = LoadKnownPackagesList(config);
            return Task.FromResult(new PackSourceChecker(providers, preFilterer, additionalDataProducers, config.SaveCandidatePacks, existingCache, knownNonTemplatePackages));
        }

        private static IEnumerable<FilteredPackageInfo>? LoadKnownPackagesList(CommandArgs config)
        {
            if (!config.DiffMode || config.DiffOverrideKnownPackagesLocation == null)
            {
                return null;
            }

            FileInfo? fileLocation = config.DiffOverrideKnownPackagesLocation;
            Verbose.WriteLine($"Opening {fileLocation.FullName}");

            using (var fileStream = fileLocation.OpenRead())
            using (var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return new JsonSerializer().Deserialize<IEnumerable<FilteredPackageInfo>>(jsonReader);
            }
        }

        private static TemplateSearchCache? LoadExistingCache(CommandArgs config)
        {
            if (!config.DiffMode || config.DiffOverrideSearchCacheLocation == null)
            {
                return null;
            }

            FileInfo? cacheFileLocation = config.DiffOverrideSearchCacheLocation;
            Verbose.WriteLine($"Opening {cacheFileLocation.FullName}");
            using (var fileStream = cacheFileLocation.OpenRead())
            using (var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return TemplateSearchCache.FromJObject(JObject.Load(jsonReader), NullLogger.Instance, null);
            }
        }
    }
}
