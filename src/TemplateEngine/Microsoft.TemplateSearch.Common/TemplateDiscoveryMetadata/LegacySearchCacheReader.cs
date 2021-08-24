// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("Use TemplateSearchCache instead.")]
    internal static class LegacySearchCacheReader
    {
        internal static TemplateSearchCache ConvertTemplateDiscoveryMetadata(TemplateDiscoveryMetadata discoveryMetadata, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders)
        {
            List<TemplatePackageSearchData> packageData = new List<TemplatePackageSearchData>();
            foreach (var package in discoveryMetadata.PackToTemplateMap)
            {
                List<TemplateSearchData> templateData = new List<TemplateSearchData>();
                foreach (var template in package.Value.TemplateIdentificationEntry)
                {
                    var foundTemplate = discoveryMetadata.TemplateCache.FirstOrDefault(t => t.Identity.Equals(template.Identity, StringComparison.OrdinalIgnoreCase));
                    if (foundTemplate == null)
                    {
                        continue;
                    }
                    if (additionalDataReaders != null)
                    {
                        Dictionary<string, object> data = new Dictionary<string, object>();
                        foreach (var reader in additionalDataReaders)
                        {
                            if (discoveryMetadata.AdditionalData.TryGetValue(reader.Key, out object? legacyData))
                            {
                                Type legacyDataType = legacyData.GetType();
                                if (legacyDataType.IsGenericType
                                    && legacyDataType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                                    && legacyDataType.GetGenericArguments()[0] == typeof(string))
                                {
                                    dynamic dict = Convert.ChangeType(legacyData, legacyDataType);
                                    if (dict.ContainsKey(template.Identity))
                                    {
                                        data[reader.Key] = dict[template.Identity];
                                    }
                                }
                            }
                        }
                        templateData.Add(new TemplateSearchData(foundTemplate, data));
                    }
                    else
                    {
                        templateData.Add(new TemplateSearchData(foundTemplate));
                    }
                }
                packageData.Add(new TemplatePackageSearchData(new PackInfo(package.Key, package.Value.Version, package.Value.TotalDownloads, package.Value.Owners, package.Value.Verified), templateData));
            }
            return new TemplateSearchCache(packageData);
        }

        internal static bool TryReadDiscoveryMetadata(IEngineEnvironmentSettings environmentSettings, string filePath, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders, out TemplateDiscoveryMetadata? discoveryMetadata)
        {
            string pathToConfig = Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, filePath);
            environmentSettings.Host.Logger.LogDebug($"Reading cache file {pathToConfig}");
            string cacheText = environmentSettings.Host.FileSystem.ReadAllText(pathToConfig);

            JObject cacheObject = JObject.Parse(cacheText);

            return TryReadDiscoveryMetadata(cacheObject, environmentSettings.Host.Logger, additionalDataReaders, out discoveryMetadata);
        }

        internal static bool TryReadDiscoveryMetadata(JObject cacheObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders, out TemplateDiscoveryMetadata? discoveryMetadata)
        {
            // add the reader calls, build the model objects
            if (TryReadVersion(logger, cacheObject, out string? version)
                && TryReadTemplateList(logger, cacheObject, out IReadOnlyList<ITemplateInfo>? templateList)
                && TryReadPackToTemplateMap(logger, cacheObject, out IReadOnlyDictionary<string, PackToTemplateEntry>? packToTemplateMap)
                && TryReadAdditionalData(logger, cacheObject, additionalDataReaders, out IReadOnlyDictionary<string, object>? additionalDta))
            {
                discoveryMetadata = new TemplateDiscoveryMetadata(version!, templateList!, packToTemplateMap!, additionalDta!);
                return true;
            }
            discoveryMetadata = null;
            return false;
        }

        private static bool TryReadVersion(ILogger logger, JObject cacheObject, out string? version)
        {
            logger.LogDebug($"Reading template metadata version");
            if (cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.Version), out JToken? value))
            {
                version = value.Value<string>();
                logger.LogDebug($"Version: {version}.");
                return true;
            }
            logger.LogDebug($"Failed to read template metadata version.");
            version = null;
            return false;
        }

        private static bool TryReadTemplateList(
            ILogger logger,
            JObject cacheObject,
            out IReadOnlyList<ITemplateInfo>? templateList)
        {
            logger.LogDebug($"Reading template list");
            try
            {
                // This is lifted from TemplateCache.ParseCacheContent - almost identical
                if (cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.TemplateCache), StringComparison.OrdinalIgnoreCase, out JToken? templateInfoToken))
                {
                    List<ITemplateInfo> buildingTemplateList = new List<ITemplateInfo>();

                    if (templateInfoToken is JArray arr)
                    {
                        foreach (JToken entry in arr)
                        {
                            if (entry != null && entry.Type == JTokenType.Object)
                            {
                                try
                                {
                                    buildingTemplateList.Add(BlobStorageTemplateInfo.FromJObject((JObject)entry));
                                }
                                catch (ArgumentException ex)
                                {
                                    logger.LogDebug($"Failed to read template info entry, missing mandatory fields. Details: {ex}");
                                }
                            }
                        }
                    }

                    logger.LogDebug($"Successfully read {buildingTemplateList.Count} templates.");
                    templateList = buildingTemplateList;
                    return true;
                }

                logger.LogDebug($"Failed to read template info entries. Details: no TemplateCache property found.");
                templateList = null;
                return false;
            }
            catch (Exception ex)
            {
                logger.LogDebug($"Failed to read template info entries. Details: {ex}");
                templateList = null;
                return false;
            }
        }

        private static bool TryReadPackToTemplateMap(ILogger logger, JObject cacheObject, out IReadOnlyDictionary<string, PackToTemplateEntry>? packToTemplateMap)
        {
            logger.LogDebug($"Reading package information.");
            try
            {
                if (!cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.PackToTemplateMap), out JToken? packToTemplateMapToken)
                    || !(packToTemplateMapToken is JObject packToTemplateMapObject))
                {
                    logger.LogDebug($"Failed to read package info entries. Details: no PackToTemplateMap property found.");
                    packToTemplateMap = null;
                    return false;
                }

                Dictionary<string, PackToTemplateEntry> workingPackToTemplateMap = new Dictionary<string, PackToTemplateEntry>();

                foreach (JProperty packEntry in packToTemplateMapObject.Properties())
                {
                    if (packEntry != null)
                    {
                        string packName = packEntry.Name.ToString();
                        JObject entryValue = (JObject)packEntry.Value;

                        if (entryValue.TryGetValue(nameof(PackToTemplateEntry.Version), StringComparison.OrdinalIgnoreCase, out JToken? versionToken)
                            && versionToken.Type == JTokenType.String
                            && entryValue.TryGetValue(nameof(PackToTemplateEntry.TemplateIdentificationEntry), StringComparison.OrdinalIgnoreCase, out JToken? identificationToken)
                            && identificationToken is JArray identificationArray)
                        {
                            string? version = versionToken.Value<string>();
                            if (version == null)
                            {
                                throw new Exception("Version value is null.");
                            }
                            List<TemplateIdentificationEntry> templatesInPack = new List<TemplateIdentificationEntry>();

                            foreach (JObject templateIdentityInfo in identificationArray)
                            {
                                string? identity = templateIdentityInfo.Value<string>(nameof(TemplateIdentificationEntry.Identity));
                                string? groupIdentity = templateIdentityInfo.Value<string>(nameof(TemplateIdentificationEntry.GroupIdentity));

                                if (identity == null)
                                {
                                    throw new Exception("Identity value is null.");
                                }
                                TemplateIdentificationEntry deserializedEntry = new TemplateIdentificationEntry(identity, groupIdentity);
                                templatesInPack.Add(deserializedEntry);
                            }

                            workingPackToTemplateMap[packName] = new PackToTemplateEntry(version, templatesInPack);
                            if (entryValue.TryGetValue(nameof(PackToTemplateEntry.TotalDownloads), out JToken? totalDownloadsToken)
                                && long.TryParse(totalDownloadsToken.Value<string>(), out long totalDownloads))
                            {
                                workingPackToTemplateMap[packName].TotalDownloads = totalDownloads;
                            }
                        }
                    }
                }

                logger.LogDebug($"Successfully read {workingPackToTemplateMap.Count} packages.");
                packToTemplateMap = workingPackToTemplateMap;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug($"Failed to read package info entries. Details: {ex}");
                packToTemplateMap = null;
                return false;
            }
        }

        private static bool TryReadAdditionalData(ILogger logger, JObject cacheObject, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders, out IReadOnlyDictionary<string, object>? additionalData)
        {
            if (additionalDataReaders == null)
            {
                additionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                return true;
            }
            logger.LogDebug($"Reading additional information.");
            // get the additional data section
            if (!cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.AdditionalData), out JToken? additionalDataToken)
                || !(additionalDataToken is JObject additionalDataObject))
            {
                logger.LogDebug($"Failed to read package info entries. Details: no AdditionalData property found.");
                additionalData = null;
                return false;
            }

            Dictionary<string, object> workingAdditionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Func<object, object>> dataReadInfo in additionalDataReaders)
            {
                try
                {
                    // get the entry for this piece of additional data
                    if (!additionalDataObject.TryGetValue(dataReadInfo.Key, StringComparison.OrdinalIgnoreCase, out JToken? dataToken)
                        || !(dataToken is JObject dataObject))
                    {
                        // this piece of data wasn't found, or wasn't valid. Ignore it.
                        continue;
                    }

                    workingAdditionalData[dataReadInfo.Key] = dataReadInfo.Value(dataObject);
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"Failed to read additional info entries. Details: {ex}");
                    // Do nothing.
                    // This piece of data failed to read, but isn't strictly necessary.
                }
            }

            logger.LogDebug($"Successfully read {workingAdditionalData.Count} additional information entries.");
            additionalData = workingAdditionalData;
            return true;
        }
    }
}
