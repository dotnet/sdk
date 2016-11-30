using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NET451
using System.Runtime.Loader;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateCache
    {
        private static IDictionary<string, ITemplate> _templateMemoryCache = new Dictionary<string, ITemplate>();

        // locale -> identity -> locator
        private static IDictionary<string, IDictionary<string, ILocalizationLocator>> _localizationMemoryCache
                = new Dictionary<string, IDictionary<string, ILocalizationLocator>>();

        public TemplateCache()
        {
            TemplateInfo = new List<TemplateInfo>();
        }

        public TemplateCache(JObject parsed)
            : this()
        {
            JToken templateInfoToken;
            if (parsed.TryGetValue("TemplateInfo", StringComparison.OrdinalIgnoreCase, out templateInfoToken))
            {
                JArray arr = templateInfoToken as JArray;
                if (arr != null)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            TemplateInfo.Add(new TemplateInfo((JObject) entry));
                        }
                    }
                }
            }
        }

        [JsonProperty]
        public List<TemplateInfo> TemplateInfo { get; set; }

        public static void Scan(IReadOnlyList<string> templateRoots)
        {
            foreach (string templateDir in templateRoots)
            {
                Scan(templateDir);
            }
        }

        // reads all the templates and langpacks for the current dir.
        // stores info about them in static members.
        // can't correctly write locale cache(s) until all of both are read.
        public static void Scan(string templateDir)
        {
            string searchTarget = Path.Combine(Directory.GetCurrentDirectory(), templateDir.Trim());
            List<string> matches = Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(searchTarget), Path.GetFileName(searchTarget), SearchOption.TopDirectoryOnly).ToList();

            if(matches.Count == 1)
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

            foreach (IMountPointFactory factory in SettingsLoader.Components.OfType<IMountPointFactory>().ToList())
            {
                IMountPoint mountPoint;
                if (factory.TryMount(null, templateDir, out mountPoint))
                {
                    ScanForComponents(mountPoint, templateDir);
                    SettingsLoader.AddMountPoint(mountPoint);

                    foreach (IGenerator generator in SettingsLoader.Components.OfType<IGenerator>())
                    {
                        IList<ILocalizationLocator> localizationInfo;
                        IEnumerable<ITemplate> templateList = generator.GetTemplatesAndLangpacksFromDir(mountPoint, out localizationInfo);

                        foreach (ILocalizationLocator locator in localizationInfo)
                        {
                            AddLocalizationToMemoryCache(locator);
                        }

                        foreach (ITemplate template in templateList)
                        {
                            AddTemplateToMemoryCache(template);
                        }
                    }
                }
            }
        }

        private static void ScanForComponents(IMountPoint mountPoint, string templateDir)
        {
            if (mountPoint.Root.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any())
            {
                string diskPath = templateDir;
                if (mountPoint.Info.MountPointFactoryId != FileSystemMountPointFactory.FactoryId)
                {
                    string path = Path.Combine(Paths.User.Content, Path.GetFileName(templateDir));

                    try
                    {
                        mountPoint.Root.CopyTo(path);
                    }
                    catch (IOException)
                    {
                        return;
                    }

                    diskPath = path;
                }

                foreach (Assembly asm in AssemblyLoader.LoadAllAssemblies(out IEnumerable<string> failures))
                {
                    try
                    {
                        foreach (Type type in asm.GetTypes())
                        {
                            SettingsLoader.Components.Register(type);
                        }

                        SettingsLoader.AddProbingPath(Path.GetDirectoryName(asm.Location));
                    }
                    catch
                    {
                    }
                }
            }
        }

        public static List<TemplateInfo> LoadTemplateCacheForLocale(string locale)
        {
            string cacheContent = Paths.User.ExplicitLocaleTemplateCacheFile(locale).ReadAllText("{}");
            JObject parsed = JObject.Parse(cacheContent);
            List<TemplateInfo> templates = new List<TemplateInfo>();

            JToken templateInfoToken;
            if (parsed.TryGetValue("TemplateInfo", StringComparison.OrdinalIgnoreCase, out templateInfoToken))
            {
                JArray arr = templateInfoToken as JArray;
                if (arr != null)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            templates.Add(new TemplateInfo((JObject)entry));
                        }
                    }
                }
            }

            return templates;
        }

        // Writes template caches for each of the following:
        //  - current locale
        //  - cultures for which new langpacks are installed
        //  - other locales with existing caches are regenerated.
        //  - neutral locale
        public static void WriteTemplateCaches()
        {
            string currentLocale = EngineEnvironmentSettings.Host.Locale;
            HashSet<string> localesWritten = new HashSet<string>();

            // If the current locale exists, always write it.
            if (! string.IsNullOrEmpty(currentLocale))
            {
                WriteTemplateCacheForLocale(currentLocale);
                localesWritten.Add(currentLocale);
            }

            // write caches for any locales which had new langpacks installed
            foreach (string langpackLocale in _localizationMemoryCache.Keys)
            {
                WriteTemplateCacheForLocale(langpackLocale);
                localesWritten.Add(langpackLocale);
            }

            // read the cache dir for other locale caches, and re-write them.
            // there may be new templates to add to them.
            string fileSearchPattern = "*." + Paths.User.TemplateCacheFileBaseName;
            foreach (string fullFilename in Paths.User.BaseDir.EnumerateFiles(fileSearchPattern, System.IO.SearchOption.TopDirectoryOnly))
            {
                string filename = Path.GetFileName(fullFilename);
                string[] fileParts = filename.Split(new char[] { '.' }, 2);
                string fileLocale = fileParts[0];

                if (!string.IsNullOrEmpty(fileLocale) &&
                    (fileParts[1] == Paths.User.TemplateCacheFileBaseName)
                    && !localesWritten.Contains(fileLocale))
                {
                    WriteTemplateCacheForLocale(fileLocale);
                    localesWritten.Add(fileLocale);
                }
            }

            // always write the culture neutral cache
            // It must be written last because when a cache for a culture is first created, it's based on the
            // culture neutral cache, plus newly registered templates.
            // If the culture neutral cache is updated before the new cache is first written,
            // the new cache will have duplicate values.
            //
            // being last may not matter anymore due to changes after the comment was written.
            WriteTemplateCacheForLocale(null);
        }

        public static void DeleteAllLocaleCacheFiles()
        {
            string fileSearchPattern = "*." + Paths.User.TemplateCacheFileBaseName;
            foreach (string fullFilename in Paths.User.BaseDir.EnumerateFiles(fileSearchPattern, System.IO.SearchOption.TopDirectoryOnly))
            {
                string filename = Path.GetFileName(fullFilename);
                string[] fileParts = filename.Split(new char[] { '.' }, 2);
                string fileLocale = fileParts[0];

                if (!string.IsNullOrEmpty(fileLocale) &&
                    (fileParts[1] == Paths.User.TemplateCacheFileBaseName))
                {
                    fullFilename.Delete();
                }
            }

            Paths.User.CultureNeutralTemplateCacheFile.Delete();
        }

        private static void WriteTemplateCacheForLocale(string locale)
        {
            bool isCurrentLocale = string.IsNullOrEmpty(locale)
                    && string.IsNullOrEmpty(EngineEnvironmentSettings.Host.Locale)
                    || (locale == EngineEnvironmentSettings.Host.Locale);

            IDictionary<string, ILocalizationLocator> locatorsForLocale;
            if (string.IsNullOrEmpty(locale)
                || !_localizationMemoryCache.TryGetValue(locale, out locatorsForLocale))
            {
                locatorsForLocale = null;
            }

            List<TemplateInfo> existingTemplatesForLocale = LoadTemplateCacheForLocale(locale);

            if (existingTemplatesForLocale.Count == 0)
            {
                // the cache for this locale didn't exist previously. Start with the neutral locale as if it were the existing
                existingTemplatesForLocale = LoadTemplateCacheForLocale(null);
            }

            HashSet<string> foundTemplates = new HashSet<string>();
            List<TemplateInfo> mergedTemplateList = new List<TemplateInfo>();

            foreach (TemplateInfo template in NewTemplateInfoForLocale(locale))
            {
                mergedTemplateList.Add(template);
                foundTemplates.Add(template.Identity);
            }

            foreach (TemplateInfo templateInfo in existingTemplatesForLocale)
            {
                if (!foundTemplates.Contains(templateInfo.Identity))
                {
                    UpdateTemplateLocalization(templateInfo, locatorsForLocale);
                    mergedTemplateList.Add(templateInfo);
                    foundTemplates.Add(templateInfo.Identity);
                }
            }

            SettingsLoader.WriteTemplateCache(mergedTemplateList, locale, isCurrentLocale);
        }

        private static void UpdateTemplateLocalization(TemplateInfo template, IDictionary<string, ILocalizationLocator> locatorsForLocale)
        {
            ILocalizationLocator localizationInfo = null;
            if (locatorsForLocale == null
                || !locatorsForLocale.TryGetValue(template.Identity, out localizationInfo))
            {
                return;
            }

            template.LocaleConfigPlace = localizationInfo.ConfigPlace ?? null;
            template.LocaleConfigMountPointId = localizationInfo.MountPointId;

            if (!string.IsNullOrEmpty(localizationInfo.Author))
            {
                template.Author = localizationInfo.Author;
            }

            if (!string.IsNullOrEmpty(localizationInfo.Name))
            {
                template.Name = localizationInfo.Name;
            }

            if (!string.IsNullOrEmpty(localizationInfo.Description))
            {
                template.Description = localizationInfo.Description;
            }
        }

        // returns TemplateInfo for all the known templates.
        // if the locale is matches localization for the template, the loc info is included.
        private static IList<TemplateInfo> NewTemplateInfoForLocale(string locale)
        {
            IList<TemplateInfo> templatesForLocale = new List<TemplateInfo>();
            IDictionary<string, ILocalizationLocator> locatorsForLocale;

            if (string.IsNullOrEmpty(locale)
                || ! _localizationMemoryCache.TryGetValue(locale, out locatorsForLocale))
            {
                locatorsForLocale = null;
            }

            foreach (ITemplate template in _templateMemoryCache.Values)
            {
                ILocalizationLocator localizationInfo = null;
                if (locatorsForLocale != null)
                {
                    locatorsForLocale.TryGetValue(template.Identity, out localizationInfo);
                }

                TemplateInfo localizedTemplate = new TemplateInfo
                {
                    GeneratorId = template.Generator.Id,
                    ConfigPlace = template.Configuration.FullPath,
                    ConfigMountPointId = template.Configuration.MountPoint.Info.MountPointId,
                    Name = localizationInfo?.Name ?? template.Name,
                    Tags = template.Tags,
                    ShortName = template.ShortName,
                    Classifications = template.Classifications,
                    Author = localizationInfo?.Author ?? template.Author,
                    Description = localizationInfo?.Description ?? template.Description,
                    GroupIdentity = template.GroupIdentity,
                    Identity = template.Identity,
                    DefaultName = template.DefaultName,
                    LocaleConfigPlace = localizationInfo?.ConfigPlace ?? null,
                    LocaleConfigMountPointId = localizationInfo?.MountPointId ?? Guid.Empty
                };

                templatesForLocale.Add(localizedTemplate);
            }

            return templatesForLocale;
        }

        // Adds the template to the memory cache, keyed on identity.
        // If the identity is the same as an existing one, it's overwritten.
        // (last in wins)
        private static void AddTemplateToMemoryCache(ITemplate template)
        {
            _templateMemoryCache[template.Identity] = template;
        }

        private static void AddLocalizationToMemoryCache(ILocalizationLocator locator)
        {
            IDictionary<string, ILocalizationLocator> localeLocators;

            if (!_localizationMemoryCache.TryGetValue(locator.Locale, out localeLocators))
            {
                localeLocators = new Dictionary<string, ILocalizationLocator>();
                _localizationMemoryCache.Add(locator.Locale, localeLocators);
            }

            localeLocators[locator.Identity] = locator;
        }
    }
}
