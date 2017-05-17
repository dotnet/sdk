using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NET45
using System.Runtime.Loader;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateCache
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
        }

        public TemplateCache(IEngineEnvironmentSettings environmentSettings, List<TemplateInfo> templatesInCache)
            :this(environmentSettings)
        {
            TemplateInfo = templatesInCache;
        }

        public TemplateCache(IEngineEnvironmentSettings environmentSettings, JObject parsed, string cacheVersion)
            : this(environmentSettings)
        {
            TemplateInfo = ParseCacheContent(parsed, cacheVersion);
        }

        [JsonProperty]
        public IReadOnlyList<TemplateInfo> TemplateInfo { get; set; }

        public IReadOnlyCollection<IFilteredTemplateInfo> List(bool exactMatchesOnly, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            HashSet<IFilteredTemplateInfo> matchingTemplates = new HashSet<IFilteredTemplateInfo>(FilteredTemplateEqualityComparer.Default);

            foreach (ITemplateInfo template in TemplateInfo)
            {
                List<MatchInfo> matchInformation = new List<MatchInfo>();

                foreach (Func<ITemplateInfo, MatchInfo?> filter in filters)
                {
                    MatchInfo? result = filter(template);

                    if (result.HasValue)
                    {
                        matchInformation.Add(result.Value);
                    }
                }

                FilteredTemplateInfo info = new FilteredTemplateInfo(template, matchInformation);
                if (info.IsMatch || (!exactMatchesOnly && info.IsPartialMatch))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NET45
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }

        public void Scan(IReadOnlyList<string> templateRoots)
        {
            foreach (string templateDir in templateRoots)
            {
                Scan(templateDir);
            }
        }

        // reads all the templates and langpacks for the current dir.
        // stores info about them in members.
        // can't correctly write locale cache(s) until all of both are read.
        public void Scan(string templateDir)
        {
            if(templateDir[templateDir.Length - 1] == '/' || templateDir[templateDir.Length - 1] == '\\')
            {
                templateDir = templateDir.Substring(0, templateDir.Length - 1);
            }

            string searchTarget = Path.Combine(_environmentSettings.Host.FileSystem.GetCurrentDirectory(), templateDir.Trim());
            List<string> matches = _environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(Path.GetDirectoryName(searchTarget), Path.GetFileName(searchTarget), SearchOption.TopDirectoryOnly).ToList();

            if (matches.Count == 1)
            {
                templateDir = matches[0];
            }
            else
            {
                foreach(string match in matches)
                {
                    Scan(match);
                }

                return;
            }

            if (_environmentSettings.SettingsLoader.TryGetMountPointFromPlace(templateDir, out IMountPoint existingMountPoint))
            {
                ScanMountPointForTemplatesAndLangpacks(existingMountPoint, templateDir);
                _environmentSettings.SettingsLoader.ReleaseMountPoint(existingMountPoint);
            }
            else
            {
                foreach (IMountPointFactory factory in _environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>().ToList())
                {
                    if (factory.TryMount(_environmentSettings, null, templateDir, out IMountPoint mountPoint))
                    {
                        //Force any local package installs into the content directory
                        if(!(mountPoint is FileSystemMountPoint))
                        {
                            string path = Path.Combine(_paths.User.Packages, Path.GetFileName(templateDir));

                            if (!string.Equals(path, templateDir))
                            {
                                _paths.CreateDirectory(_paths.User.Packages);
                                _paths.Copy(templateDir, path);

                                var attributes = _environmentSettings.Host.FileSystem.GetFileAttributes(path);
                                if (attributes.HasFlag(FileAttributes.ReadOnly))
                                {
                                    attributes &= ~FileAttributes.ReadOnly;
                                    _environmentSettings.Host.FileSystem.SetFileAttributes(path, attributes);
                                }

                                if (_environmentSettings.SettingsLoader.TryGetMountPointFromPlace(path, out IMountPoint mountPoint2) || factory.TryMount(_environmentSettings, null, path, out mountPoint2))
                                {
                                    _environmentSettings.SettingsLoader.ReleaseMountPoint(mountPoint);
                                    mountPoint = mountPoint2;
                                    templateDir = path;
                                }
                            }
                        }

                        // TODO: consider not adding the mount point if there is nothing to install.
                        // It'd require choosing to not write it upstream from here, which might be better anyway.
                        // "nothing to install" could have a couple different meanings:
                        // 1) no templates, and no langpacks were found.
                        // 2) only langpacks were found, but they aren't for any existing templates - but we won't know that at this point.
                        _environmentSettings.SettingsLoader.AddMountPoint(mountPoint);
                        if(!ScanMountPointForTemplatesAndLangpacks(mountPoint, templateDir))
                        {
                            _environmentSettings.SettingsLoader.RemoveMountPoint(mountPoint);

                            if (mountPoint.Info.Place.StartsWith(_paths.User.Packages, StringComparison.Ordinal))
                            {
                                try
                                {
                                    _environmentSettings.Host.FileSystem.FileDelete(mountPoint.Info.Place);
                                }
                                catch
                                {
                                }
                            }
                        }

                        _environmentSettings.SettingsLoader.ReleaseMountPoint(mountPoint);
                    }
                }
            }
        }

        private bool ScanMountPointForTemplatesAndLangpacks(IMountPoint mountPoint, string templateDir)
        {
            bool anythingFound = ScanForComponents(mountPoint, templateDir);

            foreach (IGenerator generator in _environmentSettings.SettingsLoader.Components.OfType<IGenerator>())
            {
                IList<ITemplate> templateList = generator.GetTemplatesAndLangpacksFromDir(mountPoint, out IList<ILocalizationLocator> localizationInfo);

                foreach (ILocalizationLocator locator in localizationInfo)
                {
                    AddLocalizationToMemoryCache(locator);
                }

                foreach (ITemplate template in templateList)
                {
                    AddTemplateToMemoryCache(template);
                }

                anythingFound |= templateList.Count > 0 || localizationInfo.Count > 0;
            }

            return anythingFound;
        }

        private bool ScanForComponents(IMountPoint mountPoint, string templateDir)
        {
            bool anythingFound = false;
            bool isInOriginalInstallLocation = true;
            if (mountPoint.Root.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any())
            {
                string diskPath = templateDir;
                if (mountPoint.Info.MountPointFactoryId != FileSystemMountPointFactory.FactoryId)
                {
                    string path = Path.Combine(_paths.User.Content, Path.GetFileName(templateDir));

                    if (templateDir.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    try
                    {
                        isInOriginalInstallLocation = false;
                        mountPoint.Root.CopyTo(path);
                    }
                    catch (IOException)
                    {
                        return false;
                    }

                    try
                    {
                        if (mountPoint.Info.Place.StartsWith(_paths.User.Packages))
                        {
                            _environmentSettings.Host.FileSystem.FileDelete(mountPoint.Info.Place);
                        }
                    }
                    catch
                    {
                    }

                    diskPath = path;
                }

                foreach (KeyValuePair<string, Assembly> asm in AssemblyLoader.LoadAllAssemblies(_paths, out IEnumerable<string> failures))
                {
                    try
                    {
                        foreach (Type type in asm.Value.GetTypes())
                        {
                            _environmentSettings.SettingsLoader.Components.Register(type);
                            anythingFound = true;
                        }

                        if (anythingFound)
                        {
                            _environmentSettings.SettingsLoader.AddProbingPath(Path.GetDirectoryName(asm.Key));
                        }
                    }
                    catch
                    {
                    }
                }

                if (!anythingFound)
                {
                    try
                    {
                        _environmentSettings.Host.FileSystem.DirectoryDelete(diskPath, true);
                    }
                    catch
                    {
                    }
                }
            }

            return isInOriginalInstallLocation && anythingFound;
        }

        // returns a list of the templates with the specified localization.
        // does not change which locale is cached in this TemplateCache instance.
        public IReadOnlyList<TemplateInfo> GetTemplatesForLocale(string locale, string existingCacheVersion)
        {
            string cacheContent = _paths.ReadAllText(_paths.User.ExplicitLocaleTemplateCacheFile(locale), "{}");

            try
            {
                JObject parsed = JObject.Parse(cacheContent);
                return ParseCacheContent(parsed, existingCacheVersion);
            }
            catch
            {
                return Empty<TemplateInfo>.List.Value;
            }
        }

        private static IReadOnlyList<TemplateInfo> ParseCacheContent(JObject contentJobject, string cacheVersion)
        {
            List<TemplateInfo> templateList = new List<TemplateInfo>();

            if (contentJobject.TryGetValue("TemplateInfo", StringComparison.OrdinalIgnoreCase, out JToken templateInfoToken))
            {
                if (templateInfoToken is JArray arr)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            templateList.Add(Settings.TemplateInfo.FromJObject((JObject)entry, cacheVersion));
                        }
                    }
                }
            }

            return templateList;
        }

        // Writes template caches for each of the following:
        //  - current locale
        //  - cultures for which new langpacks are installed
        //  - other locales with existing caches are regenerated.
        //  - neutral locale
        internal void WriteTemplateCaches(string existingCacheVersion)
        {
            string currentLocale = _environmentSettings.Host.Locale;
            HashSet<string> localesWritten = new HashSet<string>();

            // If the current locale exists, always write it.
            if (! string.IsNullOrEmpty(currentLocale))
            {
                WriteTemplateCacheForLocale(currentLocale, existingCacheVersion);
                localesWritten.Add(currentLocale);
            }

            // write caches for any locales which had new langpacks installed
            foreach (string langpackLocale in _localizationMemoryCache.Keys)
            {
                WriteTemplateCacheForLocale(langpackLocale, existingCacheVersion);
                localesWritten.Add(langpackLocale);
            }

            // read the cache dir for other locale caches, and re-write them.
            // there may be new templates to add to them.
            foreach (string locale in AllLocalesWithCacheFiles)
            {
                if (!localesWritten.Contains(locale))
                {
                    WriteTemplateCacheForLocale(locale, existingCacheVersion);
                    localesWritten.Add(locale);
                }
            }

            // always write the culture neutral cache
            // It must be written last because when a cache for a culture is first created, it's based on the
            // culture neutral cache, plus newly registered templates.
            // If the culture neutral cache is updated before the new cache is first written,
            // the new cache will have duplicate values.
            //
            // being last may not matter anymore due to changes after the comment was written.
            WriteTemplateCacheForLocale(null, existingCacheVersion);
        }

        [JsonIgnore]
        public IReadOnlyList<string> AllLocalesWithCacheFiles
        {
            get
            {
                List<string> locales = new List<string>();

                string fileSearchPattern = "*." + _paths.User.TemplateCacheFileBaseName;
                foreach (string fullFilename in _paths.EnumerateFiles(_paths.User.BaseDir, fileSearchPattern, SearchOption.TopDirectoryOnly))
                {
                    string filename = Path.GetFileName(fullFilename);
                    string[] fileParts = filename.Split(new[] { '.' }, 2);
                    string fileLocale = fileParts[0];

                    if (!string.IsNullOrEmpty(fileLocale) &&
                        (fileParts[1] == _paths.User.TemplateCacheFileBaseName))
                    {
                        locales.Add(fileLocale);
                    }
                }

                return locales;
            }
        }

        public void DeleteAllLocaleCacheFiles()
        {
            foreach (string locale in AllLocalesWithCacheFiles)
            {
                string fullFilename = _paths.User.ExplicitLocaleTemplateCacheFile(locale);
                _paths.Delete(fullFilename);
            }

            _paths.Delete(_paths.User.CultureNeutralTemplateCacheFile);
        }

        private void WriteTemplateCacheForLocale(string locale, string existingCacheVersion)
        {
            IReadOnlyList<TemplateInfo> existingTemplatesForLocale = GetTemplatesForLocale(locale, existingCacheVersion);
            IDictionary<string, ILocalizationLocator> existingLocatorsForLocale;

            if (existingTemplatesForLocale.Count == 0)
            {   // the cache for this locale didn't exist previously. Start with the neutral locale as if it were the existing (no locales)
                existingTemplatesForLocale = GetTemplatesForLocale(null, existingCacheVersion);
                existingLocatorsForLocale = new Dictionary<string, ILocalizationLocator>();
            }
            else
            {
                existingLocatorsForLocale = GetLocalizationsFromTemplates(existingTemplatesForLocale, locale);
            }

            HashSet<string> foundTemplates = new HashSet<string>();
            List<ITemplateInfo> mergedTemplateList = new List<ITemplateInfo>();

            // These are from langpacks being installed... identity -> locator
            if (string.IsNullOrEmpty(locale)
                || !_localizationMemoryCache.TryGetValue(locale, out IDictionary<string, ILocalizationLocator> newLocatorsForLocale))
            {
                newLocatorsForLocale = new Dictionary<string, ILocalizationLocator>();
            }

            foreach (ITemplate newTemplate in _templateMemoryCache.Values)
            {
                ILocalizationLocator locatorForTemplate = GetPreferredLocatorForTemplate(newTemplate.Identity, existingLocatorsForLocale, newLocatorsForLocale);
                TemplateInfo localizedTemplate = LocalizeTemplate(newTemplate, locatorForTemplate);
                mergedTemplateList.Add(localizedTemplate);
                foundTemplates.Add(newTemplate.Identity);
            }

            foreach (TemplateInfo existingTemplate in existingTemplatesForLocale)
            {
                if (!foundTemplates.Contains(existingTemplate.Identity))
                {
                    ILocalizationLocator locatorForTemplate = GetPreferredLocatorForTemplate(existingTemplate.Identity, existingLocatorsForLocale, newLocatorsForLocale);
                    TemplateInfo localizedTemplate = LocalizeTemplate(existingTemplate, locatorForTemplate);
                    mergedTemplateList.Add(localizedTemplate);
                    foundTemplates.Add(existingTemplate.Identity);
                }
            }

            _environmentSettings.SettingsLoader.WriteTemplateCache(mergedTemplateList, locale);
        }

        // find the best locator (if any). New is preferred over old
        private ILocalizationLocator GetPreferredLocatorForTemplate(string identity, IDictionary<string, ILocalizationLocator> existingLocatorsForLocale, IDictionary<string, ILocalizationLocator> newLocatorsForLocale)
        {
            if (!newLocatorsForLocale.TryGetValue(identity, out ILocalizationLocator locatorForTemplate))
            {
                existingLocatorsForLocale.TryGetValue(identity, out locatorForTemplate);
            }

            return locatorForTemplate;
        }

        private TemplateInfo LocalizeTemplate(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            TemplateInfo localizedTemplate = new TemplateInfo
            {
                GeneratorId = template.GeneratorId,
                ConfigPlace = template.ConfigPlace,
                ConfigMountPointId = template.ConfigMountPointId,
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
                LocaleConfigMountPointId = localizationInfo?.MountPointId ?? Guid.Empty,
                HostConfigMountPointId = template.HostConfigMountPointId,
                HostConfigPlace = template.HostConfigPlace,
                ThirdPartyNotices = template.ThirdPartyNotices,
                BaselineInfo = template.BaselineInfo,
            };

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
                if (localizedParameterSymbols.TryGetValue(templateTag.Key, out IParameterSymbolLocalizationModel localizationForTag))
                {   // there is loc for this symbol
                    Dictionary<string, string> localizedChoicesAndDescriptions = new Dictionary<string, string>();

                    foreach (KeyValuePair<string, string> templateChoice in templateTag.Value.ChoicesAndDescriptions)
                    {
                        if (localizationForTag.ChoicesAndDescriptions.TryGetValue(templateChoice.Key, out string localizedDesc) && !string.IsNullOrWhiteSpace(localizedDesc))
                        {
                            localizedChoicesAndDescriptions.Add(templateChoice.Key, localizedDesc);
                        }
                        else
                        {
                            localizedChoicesAndDescriptions.Add(templateChoice.Key, templateChoice.Value);
                        }
                    }

                    string tagDescription = localizationForTag.Description ?? templateTag.Value.Description;
                    ICacheTag localizedTag = new CacheTag(tagDescription, localizedChoicesAndDescriptions, templateTag.Value.DefaultValue);
                    localizedCacheTags.Add(templateTag.Key, localizedTag);
                }
                else
                {
                    localizedCacheTags.Add(templateTag.Key, templateTag.Value);
                }
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
                if (template.LocaleConfigMountPointId == null
                    || template.LocaleConfigMountPointId == Guid.Empty)
                {   // Indicates an unlocalized entry in the locale specific template cache.
                    continue;
                }

                ILocalizationLocator locator = new LocalizationLocator()
                {
                    Locale = locale,
                    MountPointId = template.LocaleConfigMountPointId,
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
