// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class TemplateCache
    {
        private readonly ILogger _logger;

        public TemplateCache(ScanResult?[] scanResults, Dictionary<string, DateTime> mountPoints, ILogger logger)
        {
            _logger = logger;
            // We need this dictionary to de-duplicate templates that have same identity
            // notice that IEnumerable<ScanResult> that we get in is order by priority which means
            // last template with same Identity wins, others are ignored...
            var templateDeduplicationDictionary = new Dictionary<string, (ITemplate Template, ILocalizationLocator? Localization)>();
            foreach (var scanResult in scanResults)
            {
                if (scanResult == null)
                {
                    continue;
                }
                foreach (ITemplate template in scanResult.Templates)
                {
                    templateDeduplicationDictionary[template.Identity] = (template, GetBestLocalizationLocatorMatch(scanResult.Localizations, template.Identity));
                }
            }

            var templates = new List<TemplateInfo>();
            foreach (var newTemplate in templateDeduplicationDictionary.Values)
            {
                templates.Add(new TemplateInfo(newTemplate.Template, newTemplate.Localization, logger));
            }

            Version = Settings.TemplateInfo.CurrentVersion;
            Locale = CultureInfo.CurrentUICulture.Name;
            TemplateInfo = templates;
            MountPointsInfo = mountPoints;
        }

        public TemplateCache(JObject? contentJobject, ILogger logger)
        {
            _logger = logger;
            if (contentJobject != null && contentJobject.TryGetValue(nameof(Version), StringComparison.OrdinalIgnoreCase, out JToken versionToken))
            {
                Version = versionToken.ToString();
            }
            else
            {
                Version = null;
                TemplateInfo = Array.Empty<TemplateInfo>();
                MountPointsInfo = new Dictionary<string, DateTime>();
                Locale = string.Empty;
                return;
            }

            if (contentJobject.TryGetValue(nameof(Locale), StringComparison.OrdinalIgnoreCase, out JToken localeToken))
            {
                Locale = localeToken.ToString();
            }
            else
            {
                Locale = string.Empty;
            }

            var mountPointInfo = new Dictionary<string, DateTime>();

            if (contentJobject.TryGetValue(nameof(MountPointsInfo), StringComparison.OrdinalIgnoreCase, out JToken mountPointInfoToken) && mountPointInfoToken is IDictionary<string, JToken> dict)
            {
                foreach (var entry in dict)
                {
                    mountPointInfo.Add(entry.Key, entry.Value.Value<DateTime>());
                }
            }

            MountPointsInfo = mountPointInfo;

            List<TemplateInfo> templateList = new List<TemplateInfo>();

            if (contentJobject.TryGetValue(nameof(TemplateInfo), StringComparison.OrdinalIgnoreCase, out JToken templateInfoToken) && templateInfoToken is JArray arr)
            {
                foreach (JToken entry in arr)
                {
                    if (entry != null && entry.Type == JTokenType.Object)
                    {
                        templateList.Add(Settings.TemplateInfo.FromJObject((JObject)entry));
                    }
                }
            }

            TemplateInfo = templateList;
        }

        [JsonProperty]
        public string? Version { get; }

        [JsonProperty]
        public string Locale { get; }

        [JsonProperty]
        public IReadOnlyList<TemplateInfo> TemplateInfo { get; }

        [JsonProperty]
        public Dictionary<string, DateTime> MountPointsInfo { get; }

        private ILocalizationLocator? GetBestLocalizationLocatorMatch(IReadOnlyList<ILocalizationLocator> localizations, string identity)
        {
            IEnumerable<ILocalizationLocator> localizationsForTemplate = localizations.Where(locator => locator.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase));

            if (!localizations.Any())
            {
                return null;
            }
            IEnumerable<string> availableLocalizations = localizationsForTemplate.Select(locator => locator.Locale);
            string? bestMatch = GetBestLocaleMatch(availableLocalizations);
            if (string.IsNullOrWhiteSpace(bestMatch))
            {
                return null;
            }
            return localizationsForTemplate.FirstOrDefault(locator => locator.Locale == bestMatch);
        }

        /// <remarks>see https://source.dot.net/#System.Private.CoreLib/ResourceFallbackManager.cs.</remarks>
        private string? GetBestLocaleMatch(IEnumerable<string> availableLocalizations)
        {
            CultureInfo currentCulture = CultureInfo.CurrentUICulture;
            do
            {
                if (availableLocalizations.Contains(currentCulture.Name, StringComparer.OrdinalIgnoreCase))
                {
                    return currentCulture.Name;
                }
                currentCulture = currentCulture.Parent;
            }
            while (currentCulture.Name != CultureInfo.InvariantCulture.Name);
            return null;
        }
    }
}
