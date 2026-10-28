// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class TemplateCache
    {
        private const string BulletSymbol = "\u2022";
        private static readonly Guid RunnableProjectGeneratorId = new("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        public TemplateCache(IReadOnlyList<ITemplatePackage> allTemplatePackages, ScanResult?[] scanResults, Dictionary<string, DateTime> mountPoints, IEngineEnvironmentSettings environmentSettings)
        {
            ILogger logger = environmentSettings.Host.Logger;

            // We need this dictionary to de-duplicate templates that have same identity
            // notice that IEnumerable<ScanResult> that we get in is order by priority which means
            // last template with same Identity will win, others will be ignored...
            var templateDeduplicationDictionary = new Dictionary<string, IList<(IScanTemplateInfo Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization, IMountPoint MountPoint)>>();
            foreach (var scanResult in scanResults)
            {
                if (scanResult == null)
                {
                    continue;
                }

                foreach (IScanTemplateInfo template in scanResult.Templates)
                {
                    var templatePackage = allTemplatePackages.FirstOrDefault(tp => tp.MountPointUri == template.MountPointUri);

                    if (templateDeduplicationDictionary.ContainsKey(template.Identity))
                    {
                        templateDeduplicationDictionary[template.Identity].Add((template, templatePackage, GetBestLocalizationLocatorMatch(template), scanResult.MountPoint));
                    }
                    else
                    {
                        templateDeduplicationDictionary[template.Identity] = new List<(IScanTemplateInfo Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization, IMountPoint)>
                        {
                            (template, templatePackage, GetBestLocalizationLocatorMatch(template), scanResult.MountPoint)
                        };
                    }
                }
            }

            var templates = new List<TemplateInfo>();
            foreach (var duplicatedIdentities in templateDeduplicationDictionary)
            {
                // last template with same Identity wins, others will be ignored due to applied deduplication logic
                (IScanTemplateInfo Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization, IMountPoint MountPoint) chosenTemplate = duplicatedIdentities.Value.Last();

                ILocalizationLocator? loc = GetBestLocalizationLocatorMatch(chosenTemplate.Template);
                (string, JsonObject?)? hostFile = GetBestHostConfigMatch(chosenTemplate.Template, environmentSettings, chosenTemplate.MountPoint);

                templates.Add(new TemplateInfo(chosenTemplate.Template, loc, hostFile));
            }

            Version = Settings.TemplateInfo.CurrentVersion;
            Locale = CultureInfo.CurrentUICulture.Name;
            TemplateInfo = templates;
            MountPointsInfo = mountPoints;

            PrintOverlappingIdentityWarning(logger, templateDeduplicationDictionary);
        }

        public TemplateCache(JsonObject? contentJObject)
        {
            if (contentJObject != null && contentJObject.TryGetValueCaseInsensitive(nameof(Version), out JsonNode? versionToken))
            {
                Version = versionToken!.ToJsonString().Trim('"');
            }
            else
            {
                Version = null;
                TemplateInfo = [];
                MountPointsInfo = new Dictionary<string, DateTime>();
                Locale = string.Empty;
                return;
            }

            Locale = contentJObject.TryGetValueCaseInsensitive(nameof(Locale), out JsonNode? localeToken)
                ? localeToken!.GetValue<string>()
                : string.Empty;

            var mountPointInfo = new Dictionary<string, DateTime>();

            if (contentJObject.TryGetValueCaseInsensitive(nameof(MountPointsInfo), out JsonNode? mountPointInfoToken) && mountPointInfoToken is JsonObject mountPointInfoObj)
            {
                foreach (var entry in mountPointInfoObj)
                {
                    if (entry.Value != null)
                    {
                        mountPointInfo.Add(entry.Key, entry.Value.GetValue<DateTime>());
                    }
                }
            }

            MountPointsInfo = mountPointInfo;

            List<TemplateInfo> templateList = new List<TemplateInfo>();

            if (contentJObject.TryGetValueCaseInsensitive(nameof(TemplateInfo), out JsonNode? templateInfoToken) && templateInfoToken is JsonArray arr)
            {
                foreach (JsonNode? entry in arr)
                {
                    if (entry is JsonObject entryObj)
                    {
                        templateList.Add(Settings.TemplateInfo.FromJObject(entryObj));
                    }
                }
            }

            TemplateInfo = templateList;
        }

        [JsonPropertyName("Version")]
        public string? Version { get; }

        [JsonPropertyName("Locale")]
        public string Locale { get; }

        [JsonPropertyName("TemplateInfo")]
        public IReadOnlyList<TemplateInfo> TemplateInfo { get; }

        [JsonPropertyName("MountPointsInfo")]
        public Dictionary<string, DateTime> MountPointsInfo { get; }

        private ILocalizationLocator? GetBestLocalizationLocatorMatch(IScanTemplateInfo template)
        {
            if (template.Localizations is null)
            {
                return null;
            }

            if (!template.Localizations.Any())
            {
                return null;
            }

            string? bestMatch = GetBestLocaleMatch(template.Localizations.Keys);
            if (string.IsNullOrWhiteSpace(bestMatch))
            {
                return null;
            }
            return template.Localizations[bestMatch!];
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
        private void PrintOverlappingIdentityWarning(ILogger logger, IDictionary<string, IList<(IScanTemplateInfo Template, ITemplatePackage TemplatePackage, ILocalizationLocator? Localization, IMountPoint)>> templateDeduplicationDictionary)
        {
            foreach (var identityToTemplates in templateDeduplicationDictionary)
            {
                // we print the message only if managed template wins and we have > 1 managed templates with overlapping identities
                var lastTemplate = identityToTemplates.Value.Last();
                var managedTemplates = identityToTemplates.Value.Where(templateInto => templateInto.TemplatePackage is IManagedTemplatePackage).ToArray();
                if (lastTemplate.TemplatePackage is IManagedTemplatePackage && managedTemplates.Length > 1)
                {
                    var templatesList = new StringBuilder();
                    foreach (var (templateName, packageId, _, _) in managedTemplates)
                    {
                        templatesList.AppendLine(string.Format(
                            LocalizableStrings.TemplatePackageManager_Warning_DetectedTemplatesIdentityConflict_Subentry,
                            BulletSymbol,
                            templateName.Name,
                            (packageId as IManagedTemplatePackage)?.DisplayName));
                    }

                    logger.LogWarning(string.Format(
                            LocalizableStrings.TemplatePackageManager_Warning_DetectedTemplatesIdentityConflict,
                            identityToTemplates.Key,
                            templatesList.ToString().TrimEnd(Environment.NewLine.ToCharArray()),
                            lastTemplate.Template.Name));
                }
            }
        }

        private (string, JsonObject?)? GetBestHostConfigMatch(IScanTemplateInfo newTemplate, IEngineEnvironmentSettings settings, IMountPoint mountPoint)
        {
            if (newTemplate.HostConfigFiles.TryGetValue(settings.Host.HostIdentifier, out string? preferredHostFilePath))
            {
                return (preferredHostFilePath, ReadHostFile(newTemplate, preferredHostFilePath, settings, mountPoint));
            }

            foreach (string fallbackHostName in settings.Host.FallbackHostTemplateConfigNames)
            {
                if (newTemplate.HostConfigFiles.TryGetValue(fallbackHostName, out string? fallbackHostFilePath))
                {
                    return (fallbackHostFilePath, ReadHostFile(newTemplate, fallbackHostFilePath, settings, mountPoint));
                }
            }
            return null;
        }

        private JsonObject? ReadHostFile(IScanTemplateInfo template, string path, IEngineEnvironmentSettings settings, IMountPoint mountPoint)
        {
            if (template.GeneratorId != RunnableProjectGeneratorId)
            {
                return null;
            }
            settings.Host.Logger.LogDebug($"Start loading host config {template.MountPointUri}{path}");
            try
            {
                IFile? hostFile = mountPoint.FileInfo(path);
                if (hostFile == null || !hostFile.Exists)
                {
                    throw new FileNotFoundException($"Host file '{hostFile?.GetDisplayPath()}' does not exist.");
                }
                return hostFile.ReadJObjectFromIFile();
            }
            catch (Exception e)
            {
                settings.Host.Logger.LogWarning(
                    e,
                    LocalizableStrings.TemplateInfo_Warning_FailedToReadHostData,
                    template.MountPointUri,
                    path);
            }
            finally
            {
                settings.Host.Logger.LogDebug($"End loading host config {template.MountPointUri}{path}");
            }
            return null;
        }

    }
}
