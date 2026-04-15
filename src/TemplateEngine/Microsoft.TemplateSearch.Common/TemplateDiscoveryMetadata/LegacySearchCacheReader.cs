// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;

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
                                IDictionary? dict = legacyData as IDictionary;
                                if (dict?.Contains(template.Identity) ?? false)
                                {
                                    data[reader.Key] = dict[template.Identity];
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
                packageData.Add(new TemplatePackageSearchData(new PackInfo(package.Key, package.Value.Version, package.Value.TotalDownloads, package.Value.Owners, package.Value.Reserved), templateData));
            }
            return new TemplateSearchCache(packageData);
        }

        internal static bool TryReadDiscoveryMetadata(IEngineEnvironmentSettings environmentSettings, string filePath, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders, out TemplateDiscoveryMetadata? discoveryMetadata)
        {
            string pathToConfig = Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, filePath);
            environmentSettings.Host.Logger.LogDebug($"Reading cache file {pathToConfig}");
            string cacheText = environmentSettings.Host.FileSystem.ReadAllText(pathToConfig);

            JsonObject cacheObject = JExtensions.ParseJsonObject(cacheText);

            return TryReadDiscoveryMetadata(cacheObject, environmentSettings.Host.Logger, additionalDataReaders, out discoveryMetadata);
        }

        internal static bool TryReadDiscoveryMetadata(JsonObject cacheObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders, out TemplateDiscoveryMetadata? discoveryMetadata)
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

        private static bool TryReadVersion(ILogger logger, JsonObject cacheObject, out string? version)
        {
            logger.LogDebug($"Reading template metadata version");
            if (cacheObject.TryGetValueCaseInsensitive(nameof(TemplateDiscoveryMetadata.Version), out JsonNode? value))
            {
                version = value?.ToString();
                logger.LogDebug($"Version: {version}.");
                return true;
            }
            logger.LogDebug($"Failed to read template metadata version.");
            version = null;
            return false;
        }

        private static bool TryReadTemplateList(
            ILogger logger,
            JsonObject cacheObject,
            out IReadOnlyList<ITemplateInfo>? templateList)
        {
            logger.LogDebug($"Reading template list");
            try
            {
                // This is lifted from TemplateCache.ParseCacheContent - almost identical
                if (cacheObject.TryGetValueCaseInsensitive(nameof(TemplateDiscoveryMetadata.TemplateCache), out JsonNode? templateInfoToken))
                {
                    List<ITemplateInfo> buildingTemplateList = new List<ITemplateInfo>();

                    if (templateInfoToken is JsonArray arr)
                    {
                        foreach (JsonNode? entry in arr)
                        {
                            if (entry is JsonObject entryObj)
                            {
                                try
                                {
                                    buildingTemplateList.Add(BlobStorageTemplateInfo.FromJObject(entryObj));
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

        private static bool TryReadPackToTemplateMap(ILogger logger, JsonObject cacheObject, out IReadOnlyDictionary<string, PackToTemplateEntry>? packToTemplateMap)
        {
            logger.LogDebug($"Reading package information.");
            try
            {
                JsonNode? packToTemplateMapToken = JExtensions.GetPropertyCaseInsensitive(cacheObject, nameof(TemplateDiscoveryMetadata.PackToTemplateMap));
                if (packToTemplateMapToken is not JsonObject packToTemplateMapObject)
                {
                    logger.LogDebug($"Failed to read package info entries. Details: no PackToTemplateMap property found.");
                    packToTemplateMap = null;
                    return false;
                }

                Dictionary<string, PackToTemplateEntry> workingPackToTemplateMap = new();

                foreach (var packEntry in packToTemplateMapObject)
                {
                    if (packEntry.Value != null)
                    {
                        string packName = packEntry.Key;
                        JsonObject entryValue = (JsonObject)packEntry.Value;

                        JsonNode? versionNode = JExtensions.GetPropertyCaseInsensitive(entryValue, nameof(PackToTemplateEntry.Version));
                        JsonNode? identificationNode = JExtensions.GetPropertyCaseInsensitive(entryValue, nameof(PackToTemplateEntry.TemplateIdentificationEntry));
                        if (versionNode is JsonValue versionVal && versionVal.GetValueKind() == JsonValueKind.String
                            && identificationNode is JsonArray identificationArray)
                        {
                            string? version = versionNode.ToString() ?? throw new Exception("Version value is null.");
                            List<TemplateIdentificationEntry> templatesInPack = new List<TemplateIdentificationEntry>();

                            foreach (JsonNode? templateIdentityInfo in identificationArray)
                            {
                                string? identity = templateIdentityInfo?.ToString(nameof(TemplateIdentificationEntry.Identity));
                                string? groupIdentity = templateIdentityInfo?.ToString(nameof(TemplateIdentificationEntry.GroupIdentity));

                                if (identity == null)
                                {
                                    throw new Exception("Identity value is null.");
                                }
                                TemplateIdentificationEntry deserializedEntry = new TemplateIdentificationEntry(identity, groupIdentity);
                                templatesInPack.Add(deserializedEntry);
                            }

                            workingPackToTemplateMap[packName] = new PackToTemplateEntry(version, templatesInPack);
                            if (entryValue.TryGetValueCaseInsensitive(nameof(PackToTemplateEntry.TotalDownloads), out JsonNode? totalDownloadsNode)
                                && long.TryParse(totalDownloadsNode?.ToString(), out long totalDownloads))
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

        private static bool TryReadAdditionalData(ILogger logger, JsonObject cacheObject, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders, out IReadOnlyDictionary<string, object>? additionalData)
        {
            if (additionalDataReaders == null)
            {
                additionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                return true;
            }
            logger.LogDebug($"Reading additional information.");
            // get the additional data section
            JsonNode? additionalDataToken = JExtensions.GetPropertyCaseInsensitive(cacheObject, nameof(TemplateDiscoveryMetadata.AdditionalData));
            if (additionalDataToken is not JsonObject additionalDataObject)
            {
                logger.LogDebug($"Failed to read package info entries. Details: no AdditionalData property found.");
                additionalData = null;
                return false;
            }

            Dictionary<string, object> workingAdditionalData = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Func<object, object>> dataReadInfo in additionalDataReaders)
            {
                try
                {
                    // get the entry for this piece of additional data
                    JsonNode? dataNode = JExtensions.GetPropertyCaseInsensitive(additionalDataObject, dataReadInfo.Key);
                    if (dataNode is not JsonObject dataObject)
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
