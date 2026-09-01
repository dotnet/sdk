// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.Filters;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;

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

            using var fileStream = fileLocation.OpenRead();
            return JsonSerializer.Deserialize<IEnumerable<FilteredPackageInfo>>(fileStream);
        }

        private static TemplateSearchCache? LoadExistingCache(CommandArgs config)
        {
            if (!config.DiffMode || config.DiffOverrideSearchCacheLocation == null)
            {
                return null;
            }

            FileInfo? cacheFileLocation = config.DiffOverrideSearchCacheLocation;
            Verbose.WriteLine($"Opening {cacheFileLocation.FullName}");
            using var fileStream = cacheFileLocation.OpenRead();
            string content = new StreamReader(fileStream, System.Text.Encoding.UTF8, true).ReadToEnd();
            JsonObject cacheObject = JExtensions.ParseJsonObject(content);
            return TemplateSearchCache.FromJObject(cacheObject, NullLogger.Instance, new Dictionary<string, Func<object, object>>() { { CliHostSearchCacheData.DataName, CliHostSearchCacheData.Reader } });
        }
    }
}
