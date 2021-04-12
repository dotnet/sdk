using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class TemplateCache
    {
        private IDictionary<string, ITemplate> _templateMemoryCache = new Dictionary<string, ITemplate>();

        // locale -> identity -> locator
        private readonly IDictionary<string, IDictionary<string, ILocalizationLocator>> _localizationMemoryCache = new Dictionary<string, IDictionary<string, ILocalizationLocator>>();
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public TemplateCache(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
            TemplateInfo = new List<TemplateInfo>();
            MountPointsInfo = new Dictionary<string, DateTime>();
            Locale = CultureInfo.CurrentUICulture.Name;
        }

        public TemplateCache(IEngineEnvironmentSettings environmentSettings, JObject parsed)
            : this(environmentSettings)
        {
            ParseCacheContent(parsed);
        }

        [JsonProperty]
        public string Version { get; private set; }

        [JsonProperty]
        public string Locale { get; private set; }

        [JsonProperty]
        public IReadOnlyList<TemplateInfo> TemplateInfo { get; set; }

        [JsonProperty]
        public Dictionary<string, DateTime> MountPointsInfo { get; set; }

        private Scanner InstallScanner
        {
            get
            {
                if (_installScanner == null)
                {
                    _installScanner = new Scanner(_environmentSettings);
                }

                return _installScanner;
            }
        }
        private Scanner _installScanner;

        private void AddTemplatesAndLangPacksFromScanResult(ScanResult scanResult)
        {
            foreach (ILocalizationLocator locator in scanResult.Localizations)
            {
                AddLocalizationToMemoryCache(locator);
            }

            foreach (ITemplate template in scanResult.Templates)
            {
                AddTemplateToMemoryCache(template);
            }
        }

        public void Scan(string installDir)
        {
            ScanResult scanResult = InstallScanner.Scan(installDir);
            AddTemplatesAndLangPacksFromScanResult(scanResult);
        }

        private void ParseCacheContent(JObject contentJobject)
        {
            if (contentJobject.TryGetValue(nameof(Version), StringComparison.OrdinalIgnoreCase, out JToken versionToken))
            {
                Version = versionToken.ToString();
            }
            else
            {
                Version = string.Empty;
            }

            if (contentJobject.TryGetValue(nameof(Locale), StringComparison.OrdinalIgnoreCase, out JToken localeToken))
            {
                Locale = localeToken.ToString();
            }


            var mountPointInfo = new Dictionary<string, DateTime>();

            if (contentJobject.TryGetValue(nameof(MountPointsInfo), StringComparison.OrdinalIgnoreCase, out JToken mountPointInfoToken))
            {
                if (mountPointInfoToken is IDictionary<string, JToken> dict)
                {
                    foreach (var entry in dict)
                    {
                        mountPointInfo.Add(entry.Key, entry.Value.ToObject<DateTime>());
                    }
                }
            }

            MountPointsInfo = mountPointInfo;

            List<TemplateInfo> templateList = new List<TemplateInfo>();

            if (contentJobject.TryGetValue(nameof(TemplateInfo), StringComparison.OrdinalIgnoreCase, out JToken templateInfoToken))
            {
                if (templateInfoToken is JArray arr)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            templateList.Add(Settings.TemplateInfo.FromJObject((JObject)entry, Version));
                        }
                    }
                }
            }

            TemplateInfo = templateList;
        }

        public void DeleteAllLocaleCacheFiles()
        {
            _paths.Delete(_paths.User.TemplateCacheFile);
        }

        public void WriteTemplateCaches(Dictionary<string, DateTime> mountPoints)
        {
            bool hasContentChanges = false;

            HashSet<string> foundTemplates = new HashSet<string>();
            List<TemplateInfo> mergedTemplateList = new List<TemplateInfo>();

            // These are from langpacks being installed... identity -> locator
            if (string.IsNullOrEmpty(Locale)
                || !_localizationMemoryCache.TryGetValue(Locale, out IDictionary<string, ILocalizationLocator> newLocatorsForLocale))
            {
                newLocatorsForLocale = new Dictionary<string, ILocalizationLocator>();
            }
            else
            {
                hasContentChanges = true;   // there are new langpacks for this locale
            }

            foreach (ITemplate newTemplate in _templateMemoryCache.Values)
            {
                ILocalizationLocator locatorForTemplate = GetPreferredLocatorForTemplate(newTemplate.Identity, newLocatorsForLocale);
                TemplateInfo localizedTemplate = LocalizeTemplate(newTemplate, locatorForTemplate);
                mergedTemplateList.Add(localizedTemplate);
                foundTemplates.Add(newTemplate.Identity);

                hasContentChanges = true;   // new template
            }

            foreach (TemplateInfo existingTemplate in TemplateInfo)
            {
                if (!foundTemplates.Contains(existingTemplate.Identity))
                {
                    mergedTemplateList.Add(existingTemplate);
                    foundTemplates.Add(existingTemplate.Identity);
                }
            }
            WriteTemplateCache(mountPoints, mergedTemplateList, hasContentChanges);
        }

        private void WriteTemplateCache(Dictionary<string, DateTime> mountPoints, List<TemplateInfo> templates, bool hasContentChanges)
        {
            bool hasMountPointChanges = false;

            for (int i = 0; i < templates.Count; ++i)
            {
                if (!mountPoints.ContainsKey(templates[i].MountPointUri))
                {
                    templates.RemoveAt(i);
                    --i;
                    hasMountPointChanges = true;
                    continue;
                }
            }

            this.Version = Settings.TemplateInfo.CurrentVersion;
            this.TemplateInfo = templates;
            this.MountPointsInfo = mountPoints;

            if (hasContentChanges || hasMountPointChanges)
            {
                JObject serialized = JObject.FromObject(this);
                _paths.WriteAllText(_paths.User.TemplateCacheFile, serialized.ToString());
            }
        }

        private ILocalizationLocator GetPreferredLocatorForTemplate(string identity, IDictionary<string, ILocalizationLocator> newLocatorsForLocale)
        {
            if (newLocatorsForLocale.TryGetValue(identity, out ILocalizationLocator locatorForTemplate))
            {
                return locatorForTemplate;
            }
            return null;
        }

        private TemplateInfo LocalizeTemplate(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            TemplateInfo localizedTemplate = new TemplateInfo
            {
                GeneratorId = template.GeneratorId,
                ConfigPlace = template.ConfigPlace,
                MountPointUri = template.MountPointUri,
                Name = localizationInfo?.Name ?? template.Name,
                Tags = LocalizeCacheTags(template, localizationInfo),
                CacheParameters = LocalizeCacheParameters(template, localizationInfo),
                ShortName = template.ShortName,
                Classifications = template.Classifications,
                Author = localizationInfo?.Author ?? template.Author,
                Description = localizationInfo?.Description ?? template.Description,
                GroupIdentity = template.GroupIdentity ?? string.Empty,
                Precedence = template.Precedence,
                Identity = template.Identity,
                DefaultName = template.DefaultName,
                LocaleConfigPlace = localizationInfo?.ConfigPlace ?? null,
                HostConfigPlace = template.HostConfigPlace,
                ThirdPartyNotices = template.ThirdPartyNotices,
                BaselineInfo = template.BaselineInfo,
                HasScriptRunningPostActions = template.HasScriptRunningPostActions
            };

            if (template is IShortNameList templateWithShortNameList)
            {
                localizedTemplate.ShortNameList = templateWithShortNameList.ShortNameList;
            }

            return localizedTemplate;
        }

        private IReadOnlyDictionary<string, ICacheTag> LocalizeCacheTags(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            if (localizationInfo == null || localizationInfo.ParameterSymbols == null)
            {
                return template.Tags;
            }

            IReadOnlyDictionary<string, ICacheTag> templateTags = template.Tags;
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> localizedParameterSymbols = localizationInfo.ParameterSymbols;

            Dictionary<string, ICacheTag> localizedCacheTags = new Dictionary<string, ICacheTag>();

            foreach (KeyValuePair<string, ICacheTag> templateTag in templateTags)
            {
                if (!localizedParameterSymbols.TryGetValue(templateTag.Key, out IParameterSymbolLocalizationModel localizationForTag))
                {
                    // There is no localization for this symbol. Use the symbol as is.
                    localizedCacheTags.Add(templateTag.Key, templateTag.Value);
                    continue;
                }

                // There is localization. Create a localized instance, starting with the choices.
                var localizedChoices = new Dictionary<string, ParameterChoice>();

                foreach (KeyValuePair<string, ParameterChoice> templateChoice in templateTag.Value.Choices)
                {
                    ParameterChoice localizedChoice = new ParameterChoice(
                        templateChoice.Value.DisplayName,
                        templateChoice.Value.Description);

                    if (localizationForTag.Choices.TryGetValue(templateChoice.Key, out ParameterChoiceLocalizationModel locModel))
                    {
                        localizedChoice.Localize(locModel);
                    }

                    localizedChoices.Add(templateChoice.Key, localizedChoice);
                }

                ICacheTag localizedTag = new CacheTag(
                    localizationForTag.DisplayName ?? templateTag.Value.DisplayName,
                    localizationForTag.Description ?? templateTag.Value.Description,
                    localizedChoices,
                    templateTag.Value.DefaultValue,
                    (templateTag.Value as IAllowDefaultIfOptionWithoutValue)?.DefaultIfOptionWithoutValue);

                localizedCacheTags.Add(templateTag.Key, localizedTag);
            }

            return localizedCacheTags;
        }

        private IReadOnlyDictionary<string, ICacheParameter> LocalizeCacheParameters(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            if (localizationInfo == null || localizationInfo.ParameterSymbols == null)
            {
                return template.CacheParameters;
            }

            IReadOnlyDictionary<string, ICacheParameter> templateCacheParameters = template.CacheParameters;
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> localizedParameterSymbols = localizationInfo.ParameterSymbols;


            Dictionary<string, ICacheParameter> localizedCacheParams = new Dictionary<string, ICacheParameter>();

            foreach (KeyValuePair<string, ICacheParameter> templateParam in templateCacheParameters)
            {
                if (localizedParameterSymbols.TryGetValue(templateParam.Key, out IParameterSymbolLocalizationModel localizationForParam))
                {   // there is loc info for this symbol
                    ICacheParameter localizedParam = new CacheParameter
                    {
                        DataType = templateParam.Value.DataType,
                        DefaultValue = templateParam.Value.DefaultValue,
                        DisplayName = localizationForParam.DisplayName ?? templateParam.Value.DisplayName,
                        Description = localizationForParam.Description ?? templateParam.Value.Description
                    };

                    localizedCacheParams.Add(templateParam.Key, localizedParam);
                }
                else
                {
                    localizedCacheParams.Add(templateParam.Key, templateParam.Value);
                }
            }

            return localizedCacheParams;
        }

        // return dict is: Identity -> locator
        private IDictionary<string, ILocalizationLocator> GetLocalizationsFromTemplates(IReadOnlyList<TemplateInfo> templateList, string locale)
        {
            IDictionary<string, ILocalizationLocator> locatorLookup = new Dictionary<string, ILocalizationLocator>();

            foreach (TemplateInfo template in templateList)
            {
                if (string.IsNullOrEmpty(template.LocaleConfigPlace))
                {   // Indicates an unlocalized entry in the locale specific template cache.
                    continue;
                }

                ILocalizationLocator locator = new LocalizationLocator()
                {
                    Locale = locale,
                    MountPointUri = template.MountPointUri,
                    ConfigPlace = template.LocaleConfigPlace,
                    Identity = template.Identity,
                    Author = template.Author,
                    Name = template.Name,
                    Description = template.Description
                    // ParameterSymbols are not needed here. If things get refactored too much, they might become needed
                };

                locatorLookup.Add(locator.Identity, locator);
            }

            return locatorLookup;
        }

        // Adds the template to the memory cache, keyed on identity.
        // If the identity is the same as an existing one, it's overwritten.
        // (last in wins)
        private void AddTemplateToMemoryCache(ITemplate template)
        {
            _templateMemoryCache[template.Identity] = template;
        }

        private void AddLocalizationToMemoryCache(ILocalizationLocator locator)
        {
            if (!_localizationMemoryCache.TryGetValue(locator.Locale, out IDictionary<string, ILocalizationLocator> localeLocators))
            {
                localeLocators = new Dictionary<string, ILocalizationLocator>();
                _localizationMemoryCache.Add(locator.Locale, localeLocators);
            }

            localeLocators[locator.Identity] = locator;
        }
    }
}
