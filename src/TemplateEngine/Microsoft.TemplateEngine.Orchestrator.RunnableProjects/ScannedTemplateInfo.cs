// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// This class represent the template discovered during scanning. It is not ready to be run.
    /// It also returns information about all the localizations available for the template and all host files available for the template.
    /// Before running the template the caller should select localization and host file to use.
    /// Use <see cref="RunnableProjectConfig"/> to load template for running.
    /// </summary>
    internal class ScannedTemplateInfo : DirectoryBasedTemplate, IScanTemplateInfo
    {
        private readonly IReadOnlyDictionary<string, IFile> _hostConfigFiles;
        private readonly IReadOnlyDictionary<CultureInfo, TemplateLocalizationInfo> _localizations;

        /// <summary>
        /// Creates instance of the class based on configuration from <paramref name="templateFile"/>.
        /// </summary>
        public ScannedTemplateInfo(IEngineEnvironmentSettings settings, IGenerator generator, IFile templateFile) : base(settings, generator, templateFile)
        {
            _hostConfigFiles = FindHostTemplateConfigFiles();
            _localizations = FindLocalizations();
        }

        IReadOnlyDictionary<string, ILocalizationLocator> IScanTemplateInfo.Localizations => _localizations.ToDictionary(l => l.Key.Name, l => (ILocalizationLocator)l.Value);

        public IReadOnlyDictionary<string, string> HostConfigFiles => _hostConfigFiles.ToDictionary(f => f.Key, f => f.Value.FullPath);

        public override IFile ConfigFile => base.ConfigFile ?? throw new InvalidOperationException();

        public override IDirectory ConfigDirectory => base.ConfigDirectory ?? throw new InvalidOperationException();

        internal override IReadOnlyDictionary<CultureInfo, TemplateLocalizationInfo> Localizations => _localizations;

        internal override IReadOnlyDictionary<string, IFile> HostFiles => _hostConfigFiles;

        /// <summary>
        /// Attempts to find the host configuration files.
        /// </summary>
        private IReadOnlyDictionary<string, IFile> FindHostTemplateConfigFiles()
        {
            Dictionary<string, IFile> allHostFilesForTemplate = new();

            foreach (IFile hostFile in ConfigDirectory.EnumerateFiles($"*{HostTemplateFileConfigBaseName}", SearchOption.TopDirectoryOnly))
            {
                allHostFilesForTemplate.Add(ParseHostFileName(hostFile.Name), hostFile);
                Logger.LogDebug($"Found host configuration file at {hostFile.GetDisplayPath()}.");
            }

            return allHostFilesForTemplate;
        }

        private IReadOnlyDictionary<CultureInfo, TemplateLocalizationInfo> FindLocalizations()
        {
            Dictionary<CultureInfo, TemplateLocalizationInfo> localizations = new();
            IDirectory? localizeFolder = ConfigDirectory.DirectoryInfo("localize");
            if (localizeFolder == null || !localizeFolder.Exists)
            {
                return localizations;
            }

            foreach (IFile locFile in localizeFolder.EnumerateFiles(LocalizationFilePrefix + "*" + LocalizationFileExtension, SearchOption.AllDirectories))
            {
                CultureInfo? locale = ParseLocFileName(locFile);
                if (locale == null)
                {
                    Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, locFile.GetDisplayPath());
                    continue;
                }
                try
                {
                    LocalizationModel locModel = LocalizationModelDeserializer.Deserialize(locFile);
                    localizations[locale] = new TemplateLocalizationInfo(locale, locModel, locFile);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, locFile.GetDisplayPath());
                    Logger.LogDebug("Details: {0}", ex);
                }
            }
            return localizations;
        }
    }
}
