// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class TemplateCache
    {
        public TemplateCache(ScanResult?[] scanResults, Dictionary<string, DateTime> mountPoints)
        {
            // We need this dictionary to de-duplicate templates that have same identity
            // notice that IEnumerable<ScanResult> that we get in is order by priority which means
            // last template with same Identity wins, others are ignored...
            var templateDeduplicationDictionary = new Dictionary<string, ITemplate>();
            var localizationsByTemplateId = new Dictionary<string, ILocalizationLocator>();

            string uiLocale = CultureInfo.CurrentUICulture.Name;
            string uiLocaleWithoutCountry = GetLocaleNameWithoutCountry(uiLocale);

            foreach (var scanResult in scanResults)
            {
                if (scanResult == null)
                {
                    continue;
                }
                foreach (ILocalizationLocator locator in scanResult.Localizations)
                {
                    if (uiLocale != locator.Locale &&
                        localizationsByTemplateId.TryGetValue(locator.Identity, out ILocalizationLocator existingLoc) &&
                        existingLoc.Locale == GetLocaleNameWithoutCountry(existingLoc.Locale))
                    {
                        // This localization is not a perfect match and we already have the localizations for countryless locale available. Ignore this one.
                        continue;
                    }

                    string templateLocaleWithoutCountry = GetLocaleNameWithoutCountry(locator.Locale);
                    if (uiLocaleWithoutCountry != templateLocaleWithoutCountry)
                    {
                        // UI is "fr", but the localizations are for "en". This localization is not good enough to be a substitute.
                        continue;
                    }

                    // This localization is either the perfect match, or a suitable substitute and there are no other candidates for the job yet.
                    localizationsByTemplateId[locator.Identity] = locator;
                }

                foreach (ITemplate template in scanResult.Templates)
                {
                    templateDeduplicationDictionary[template.Identity] = template;
                }
            }

            var templates = new List<TemplateInfo>();
            foreach (ITemplate newTemplate in templateDeduplicationDictionary.Values)
            {
                localizationsByTemplateId.TryGetValue(newTemplate.Identity, out ILocalizationLocator locatorForTemplate);
                templates.Add(new TemplateInfo(newTemplate, locatorForTemplate));
            }

            Version = Settings.TemplateInfo.CurrentVersion;
            Locale = uiLocale;
            TemplateInfo = templates;
            MountPointsInfo = mountPoints;
        }

        public TemplateCache(JObject contentJobject)
        {
            if (contentJobject.TryGetValue(nameof(Version), StringComparison.OrdinalIgnoreCase, out JToken versionToken))
            {
                Version = versionToken.ToString();
            }
            else
            {
                Version = null;
                TemplateInfo = new List<TemplateInfo>();
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

        /// <summary>
        /// Given a locale, removes the country/region part and returns.
        /// </summary>
        /// <param name="locale">Locale in format "language-countryRegion".</param>
        /// <examples>
        /// <list type="bullet">
        /// <item>Input "en-US" returns "en".</item>
        /// <item>Input "de-AU" returns "de".</item>
        /// <item>Input "de" returns "de".</item>
        /// </list>
        /// </examples>
        private static string GetLocaleNameWithoutCountry(string locale)
        {
            int countrySplitterIndex = locale.IndexOf('-');
            if (countrySplitterIndex != -1)
            {
                return locale.Substring(0, countrySplitterIndex);
            }

            // This locale doesn't have country/Region to begin with.
            return locale;
        }
    }
}
