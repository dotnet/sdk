// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class TemplateCache
    {
        private const string BulletSymbol = "\u2022";

        private readonly ILogger _logger;

        public TemplateCache(IReadOnlyList<ITemplatePackage> allTemplatePackages, ScanResult?[] scanResults, Dictionary<string, DateTime> mountPoints, ILogger logger)
        {
            _logger = logger;

            // We need this dictionary to de-duplicate templates that have same identity
            // notice that IEnumerable<ScanResult> that we get in is order by priority which means
            // last template with same Identity will win, others will be ignored...
            var templateDeduplicationDictionary = new Dictionary<string, IList<(ITemplate Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization)>>();
            foreach (var scanResult in scanResults)
            {
                if (scanResult == null)
                {
                    continue;
                }

                foreach (ITemplate template in scanResult.Templates)
                {
                    var templatePackage = allTemplatePackages.FirstOrDefault(tp => tp.MountPointUri == template.MountPointUri);

                    if (templateDeduplicationDictionary.ContainsKey(template.Identity))
                    {
                        templateDeduplicationDictionary[template.Identity].Add((template, templatePackage, GetBestLocalizationLocatorMatch(scanResult.Localizations, template.Identity)));
                    }
                    else
                    {
                        templateDeduplicationDictionary[template.Identity] = new List<(ITemplate Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization)>
                        {
                            (template, templatePackage, GetBestLocalizationLocatorMatch(scanResult.Localizations, template.Identity))
                        };
                    }
                }
            }

            var templates = new List<TemplateInfo>();
            foreach (var duplicatedIdentities in templateDeduplicationDictionary)
            {
                // last template with same Identity wins, others will be ignored due to applied deduplication logic
                var newTemplate = duplicatedIdentities.Value.Last();
                templates.Add(new TemplateInfo(newTemplate.Template, newTemplate.Localization, logger));
            }

            Version = Settings.TemplateInfo.CurrentVersion;
            Locale = CultureInfo.CurrentUICulture.Name;
            TemplateInfo = templates;
            MountPointsInfo = mountPoints;

            PrintOverlappingIdentityWarning(templateDeduplicationDictionary);
        }

        public TemplateCache(JObject? contentJobject, ILogger logger)
        {
            _logger = logger;
            if (contentJobject != null && contentJobject.TryGetValue(nameof(Version), StringComparison.OrdinalIgnoreCase, out JToken? versionToken))
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

            Locale = contentJobject.TryGetValue(nameof(Locale), StringComparison.OrdinalIgnoreCase, out JToken? localeToken)
                ? localeToken.ToString()
                : string.Empty;

            var mountPointInfo = new Dictionary<string, DateTime>();

            if (contentJobject.TryGetValue(nameof(MountPointsInfo), StringComparison.OrdinalIgnoreCase, out JToken? mountPointInfoToken) && mountPointInfoToken is IDictionary<string, JToken> dict)
            {
                foreach (var entry in dict)
                {
                    mountPointInfo.Add(entry.Key, entry.Value.Value<DateTime>());
                }
            }

            MountPointsInfo = mountPointInfo;

            List<TemplateInfo> templateList = new List<TemplateInfo>();

            if (contentJobject.TryGetValue(nameof(TemplateInfo), StringComparison.OrdinalIgnoreCase, out JToken? templateInfoToken) && templateInfoToken is JArray arr)
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

        // add warning for the case when there is an attempt to overwrite existing managed by new managed template
        private void PrintOverlappingIdentityWarning(IDictionary<string, IList<(ITemplate Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization)>> templateDeduplicationDictionary)
        {
            foreach (var identityToTemplates in templateDeduplicationDictionary)
            {
                // we print the message only if managed template wins and we have > 1 managed templates with overlapping identities
                var lastTemplate = identityToTemplates.Value.Last();
                var managedTemplates = identityToTemplates.Value.Where(templateInto => templateInto.TemplatePackage is IManagedTemplatePackage).Except(new[] { lastTemplate });
                if (lastTemplate.TemplatePackage is IManagedTemplatePackage managedPackage && managedTemplates.Any())
                {
                    var templatesList = new StringBuilder();
                    foreach (var (templateName, packageId, _) in managedTemplates)
                    {
                        templatesList.AppendLine(string.Format(
                            LocalizableStrings.TemplatePackageManager_Warning_DetectedTemplatesIdentityConflict_Subentry,
                            BulletSymbol,
                            templateName.Name,
                            (packageId as IManagedTemplatePackage)?.DisplayName));
                    }

                    _logger.LogWarning(string.Format(
                            LocalizableStrings.TemplatePackageManager_Warning_DetectedTemplatesIdentityConflict,
                            identityToTemplates.Key,
                            templatesList.ToString().TrimEnd(Environment.NewLine.ToCharArray()),
                            lastTemplate.Template.Name));
                }
            }
        }
    }
}
