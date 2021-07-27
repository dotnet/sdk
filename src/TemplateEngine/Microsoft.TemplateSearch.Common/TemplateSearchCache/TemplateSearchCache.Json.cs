// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    internal partial class TemplateSearchCache
    {
        [JsonIgnore]
        private static readonly string[] _supportedVersions = new[] { "1.0.0.0", "1.0.0.3", "2.0" };

        internal static TemplateSearchCache FromJObject(
            JObject cacheObject,
            ILogger logger,
            IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            if (cacheObject is null)
            {
                throw new ArgumentNullException(nameof(cacheObject));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (!TryReadVersion(logger, cacheObject, out string? version) || string.IsNullOrWhiteSpace(version))
            {
                throw new NotSupportedException(LocalizableStrings.TemplateSearchCache_Exception_NotSupported);
            }
            if (version!.StartsWith("1"))
            {
#pragma warning disable CS0612, CS0618 // Type or member is obsolete
                if (LegacySearchCacheReader.TryReadDiscoveryMetadata(cacheObject, logger, additionalDataReaders, out TemplateDiscoveryMetadata? discoveryMetadata))
                {
                    return LegacySearchCacheReader.ConvertTemplateDiscoveryMetadata(discoveryMetadata!, additionalDataReaders);
                }
#pragma warning restore CS0612, CS0618 // Type or member is obsolete

            }

            JArray? data = cacheObject.Get<JArray>(nameof(TemplatePackages));
            if (data == null)
            {
                throw new Exception(LocalizableStrings.TemplateSearchCache_Exception_NotValid);
            }
            List<TemplatePackageSearchData> templatePackages = new List<TemplatePackageSearchData>();
            foreach (JToken templatePackage in data)
            {
                JObject? templatePackageObj = templatePackage as JObject;
                try
                {
                    if (templatePackageObj == null)
                    {
                        throw new Exception($"Unexpected data in template search cache data, property: {nameof(TemplatePackages)}, value: {templatePackage}");
                    }
                    templatePackages.Add(new TemplatePackageSearchData(templatePackageObj, logger, additionalDataReaders));
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"Failed to read template package data {templatePackage}, details: {ex}");
                }
            }
            return new TemplateSearchCache(templatePackages, version!);
        }

        internal static IDictionary<string, object> ReadAdditionalData(
            JObject cacheObject,
            IReadOnlyDictionary<string, Func<object, object>> additionalDataReaders,
            ILogger logger)
        {
            Dictionary<string, object> additionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Func<object, object>> dataReadInfo in additionalDataReaders)
            {
                if (!cacheObject.TryGetValue(dataReadInfo.Key, StringComparison.OrdinalIgnoreCase, out JToken dataToken)
                    || !(dataToken is JObject dataObject))
                {
                    // this piece of data wasn't found, or wasn't valid. Ignore it.
                    continue;
                }
                try
                {
                    // get the entry for this piece of additional data
                    additionalData[dataReadInfo.Key] = dataReadInfo.Value(dataObject);
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"Failed to read additional info entries. Details: {ex}");
                    // Do nothing.
                    // This piece of data failed to read, but isn't strictly necessary.
                }
            }

            logger.LogDebug($"Successfully read {additionalData.Count} additional information entries.");
            return additionalData;
        }

        internal JObject ToJObject()
        {
            return JObject.FromObject(this);
        }

        private static bool TryReadVersion(ILogger logger, JObject cacheObject, out string? version)
        {
            logger.LogDebug($"Reading template search cache version");
            version = cacheObject.ToString(nameof(Version));
            if (!string.IsNullOrWhiteSpace(version))
            {
                logger.LogDebug($"Version: {version}.");
                if (_supportedVersions.Contains(version))
                {
                    return true;
                }
                else
                {
                    logger.LogDebug($"Unsupported template search cache version.");
                    version = null;
                    return false;
                }
            }
            logger.LogDebug($"Failed to read template search cache version.");
            version = null;
            return false;
        }
    }
}
