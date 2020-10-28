using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    public static class FileMetadataTemplateSearchCacheReader
    {
        public static bool TryReadDiscoveryMetadata(IEngineEnvironmentSettings environment, ISearchCacheConfig config, out TemplateDiscoveryMetadata discoveryMetadata)
        {
            Paths paths = new Paths(environment);
            string pathToConfig = Path.Combine(paths.User.BaseDir, config.TemplateDiscoveryFileName);
            string cacheText = paths.ReadAllText(pathToConfig);

            JObject cacheObject = JObject.Parse(cacheText);

            // add the reader calls, build the model objects
            if (TryReadVersion(cacheObject, out string version)
                && TryReadTemplateList(cacheObject, version, out IReadOnlyList<ITemplateInfo> templateList)
                && TryReadPackToTemplateMap(cacheObject, out IReadOnlyDictionary<string, PackToTemplateEntry> packToTemplateMap)
                && TryReadAdditionalData(cacheObject, config.AdditionalDataReaders, out IReadOnlyDictionary<string, object> additionalDta))
            {
                discoveryMetadata = new TemplateDiscoveryMetadata(version, templateList, packToTemplateMap, additionalDta);
                return true;
            }

            discoveryMetadata = null;
            return false;
        }

        private static bool TryReadVersion(JObject cacheObject, out string version)
        {
            if (cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.Version), out JToken value))
            {
                version = value.Value<string>();
                return true;
            }

            version = null;
            return false;
        }

        private static bool TryReadTemplateList(JObject cacheObject, string cacheVersion, out IReadOnlyList<ITemplateInfo> templateList)
        {
            try
            {
                // This is lifted from TemplateCache.ParseCacheContent - almost identical
                if (cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.TemplateCache), StringComparison.OrdinalIgnoreCase, out JToken templateInfoToken))
                {
                    List<ITemplateInfo> buildingTemplateList = new List<ITemplateInfo>();

                    if (templateInfoToken is JArray arr)
                    {
                        foreach (JToken entry in arr)
                        {
                            if (entry != null && entry.Type == JTokenType.Object)
                            {
                                buildingTemplateList.Add(TemplateInfo.FromJObject((JObject)entry, cacheVersion));
                            }
                        }
                    }

                    templateList = buildingTemplateList;
                    return true;
                }

                templateList = null;
                return false;
            }
            catch
            {
                templateList = null;
                return false;
            }
        }

        private static bool TryReadPackToTemplateMap(JObject cacheObject, out IReadOnlyDictionary<string, PackToTemplateEntry> packToTemplateMap)
        {
            try
            {
                if (!cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.PackToTemplateMap), out JToken packToTemplateMapToken)
                    || !(packToTemplateMapToken is JObject packToTemplateMapObject))
                {
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

                        if (entryValue.TryGetValue(nameof(PackToTemplateEntry.Version), StringComparison.OrdinalIgnoreCase, out JToken versionToken)
                            && versionToken.Type == JTokenType.String
                            && entryValue.TryGetValue(nameof(PackToTemplateEntry.TemplateIdentificationEntry), StringComparison.OrdinalIgnoreCase, out JToken identificationToken)
                            && identificationToken is JArray identificationArray)
                        {
                            string version = versionToken.Value<string>();
                            List<TemplateIdentificationEntry> templatesInPack = new List<TemplateIdentificationEntry>();

                            foreach (JObject templateIdentityInfo in identificationArray)
                            {
                                string identity = templateIdentityInfo.Value<string>(nameof(TemplateIdentificationEntry.Identity));
                                string groupIdentity = templateIdentityInfo.Value<string>(nameof(TemplateIdentificationEntry.GroupIdentity));

                                TemplateIdentificationEntry deserializedEntry = new TemplateIdentificationEntry(identity, groupIdentity);
                                templatesInPack.Add(deserializedEntry);
                            }

                            workingPackToTemplateMap[packName] = new PackToTemplateEntry(version, templatesInPack);
                            if (entryValue.TryGetValue(nameof(PackToTemplateEntry.TotalDownloads), out JToken totalDownloadsToken)
                                && long.TryParse(totalDownloadsToken.Value<string>(), out long totalDownloads))
                            {
                                workingPackToTemplateMap[packName].TotalDownloads = totalDownloads;
                            }
                        }
                    }
                }

                packToTemplateMap = workingPackToTemplateMap;
                return true;
            }
            catch
            {
                packToTemplateMap = null;
                return false;
            }
        }

        private static bool TryReadAdditionalData(JObject cacheObject, IReadOnlyDictionary<string, Func<JObject, object>> additionalDataReaders, out IReadOnlyDictionary<string, object> additionalData)
        {
            // get the additional data section
            if (!cacheObject.TryGetValue(nameof(TemplateDiscoveryMetadata.AdditionalData), out JToken additionalDataToken)
                || !(additionalDataToken is JObject additionalDataObject))
            {
                additionalData = null;
                return false;
            }

            Dictionary<string, object> workingAdditionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Func<JObject, object>> dataReadInfo in additionalDataReaders)
            {
                try
                {
                    // get the entry for this piece of additional data
                    if (!additionalDataObject.TryGetValue(dataReadInfo.Key, StringComparison.OrdinalIgnoreCase, out JToken dataToken)
                        || !(dataToken is JObject dataObject))
                    {
                        // this piece of data wasn't found, or wasn't valid. Ignore it.
                        continue;
                    }

                    workingAdditionalData[dataReadInfo.Key] = dataReadInfo.Value(dataObject);
                }
                catch
                {
                    // Do nothing.
                    // This piece of data failed to read, but isn't strictly necessary.
                }
            }

            additionalData = workingAdditionalData;
            return true;
        }
    }
}
