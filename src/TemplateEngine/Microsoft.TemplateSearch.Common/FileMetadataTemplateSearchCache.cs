// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public class FileMetadataTemplateSearchCache : IFileMetadataTemplateSearchCache
    {
        private readonly IEngineEnvironmentSettings _environment;
        private ISearchCacheConfig _config;

        public FileMetadataTemplateSearchCache(IEngineEnvironmentSettings environmentSettings, string pathToMetadata)
        {
            _environment = environmentSettings;
            PathToMetadta = pathToMetadata;
            IsInitialized = false;
        }

        protected string PathToMetadta { get; }

        protected bool IsInitialized { get; set; }

        protected TemplateDiscoveryMetadata TemplateDiscoveryMetadata { get; set; }

        protected TemplateToPackMap TemplateToPackMap { get; set; }

        public IReadOnlyList<ITemplateInfo> GetNameMatchedTemplates(string searchName)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(searchName))
            {
                return TemplateDiscoveryMetadata.TemplateCache;
            }

            return TemplateDiscoveryMetadata.TemplateCache.Where(
                template => template.Name.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0
                || template.ShortNameList.Any(shortName => shortName.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        public IReadOnlyDictionary<string, PackInfo> GetTemplateToPackMapForTemplateIdentities(IReadOnlyList<string> identities)
        {
            EnsureInitialized();

            Dictionary<string, PackInfo> map = new Dictionary<string, PackInfo>();

            foreach (string templateIdentity in identities)
            {
                if (TemplateToPackMap.TryGetPackInfoForTemplateIdentity(templateIdentity, out PackInfo packInfo))
                {
                    map[templateIdentity] = packInfo;
                }
            }

            return map;
        }

        public IReadOnlyDictionary<string, PackToTemplateEntry> GetInfoForNamedPacks(IReadOnlyList<string> packNameList)
        {
            EnsureInitialized();

            Dictionary<string, PackToTemplateEntry> packInfo = new Dictionary<string, PackToTemplateEntry>();

            foreach (string packName in packNameList)
            {
                if (TemplateDiscoveryMetadata.PackToTemplateMap.TryGetValue(packName, out PackToTemplateEntry packToTemplateEntry))
                {
                    packInfo[packName] = packToTemplateEntry;
                }
            }

            return packInfo;
        }

        protected virtual NuGetSearchCacheConfig SetupSearchCacheConfig()
        {
            return new NuGetSearchCacheConfig(PathToMetadta);
        }

        protected virtual void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }

            _config = SetupSearchCacheConfig();

            if (FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(_environment, _config, out TemplateDiscoveryMetadata metadata))
            {
                TemplateDiscoveryMetadata = metadata;
                TemplateToPackMap = TemplateToPackMap.FromPackToTemplateDictionary(TemplateDiscoveryMetadata.PackToTemplateMap);
                IsInitialized = true;
            }
            else
            {
                throw new Exception("Error reading template search metadata");
            }
        }
    }
}
