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
        protected readonly string _pathToMetadta;
        private ISearchCacheConfig _config;
        protected bool _isInitialized;
        protected TemplateDiscoveryMetadata _templateDiscoveryMetadata;
        protected TemplateToPackMap _templateToPackMap;

        public FileMetadataTemplateSearchCache(IEngineEnvironmentSettings environmentSettings, string pathToMetadata)
        {
            _environment = environmentSettings;
            _pathToMetadta = pathToMetadata;
            _isInitialized = false;
        }

        protected virtual NuGetSearchCacheConfig SetupSearchCacheConfig()
        {
            return new NuGetSearchCacheConfig(_pathToMetadta);
        }

        protected virtual void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            _config = SetupSearchCacheConfig();

            if (FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(_environment, _config, out _templateDiscoveryMetadata))
            {
                _templateToPackMap = TemplateToPackMap.FromPackToTemplateDictionary(_templateDiscoveryMetadata.PackToTemplateMap);
                _isInitialized = true;
            }
            else
            {
                throw new Exception("Error reading template search metadata");
            }
        }

        public IReadOnlyList<ITemplateInfo> GetNameMatchedTemplates(string searchName)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(searchName))
            {
                return _templateDiscoveryMetadata.TemplateCache;
            }

            return _templateDiscoveryMetadata.TemplateCache.Where(
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
                if (_templateToPackMap.TryGetPackInfoForTemplateIdentity(templateIdentity, out PackInfo packInfo))
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
                if (_templateDiscoveryMetadata.PackToTemplateMap.TryGetValue(packName, out PackToTemplateEntry packToTemplateEntry))
                {
                    packInfo[packName] = packToTemplateEntry;
                }
            }

            return packInfo;
        }
    }
}
