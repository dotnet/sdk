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
    /// It also returns information about all the localizations available for the template and all host files avaialble for the template.
    /// Before running the template the caller should select localization and host file to use.
    /// Use <see cref="RunnableProjectConfig"/> to load template for running.
    /// </summary>
    internal class ScannedTemplateInfo : DirectoryBasedTemplate, IScanTemplateInfo
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        internal const string LocalizationFilePrefix = "templatestrings.";
        internal const string LocalizationFileExtension = ".json";

        private readonly IReadOnlyDictionary<string, IFile> _hostConfigFiles;
        private readonly IReadOnlyDictionary<CultureInfo, TemplateLocalization> _localizations;

        /// <summary>
        /// Creates instance of the class based on configuration from <paramref name="templateFile"/>.
        /// </summary>
        public ScannedTemplateInfo(IEngineEnvironmentSettings settings, IGenerator generator, IFile templateFile) : base(settings, generator, templateFile)
        {
            _hostConfigFiles = FindHostTemplateConfigFiles();
            _localizations = FindLocalizations();
        }

        public IReadOnlyDictionary<string, ILocalizationLocator> Localizations => _localizations.ToDictionary(l => l.Key.Name, l => (ILocalizationLocator)l.Value);

        public IReadOnlyDictionary<string, string> HostConfigFiles => _hostConfigFiles.ToDictionary(f => f.Key, f => f.Value.FullPath);

        public override IFile ConfigFile => base.ConfigFile ?? throw new InvalidOperationException();

        public override IDirectory ConfigDirectory => base.ConfigDirectory ?? throw new InvalidOperationException();

        /// <summary>
        /// Attempts to find the host configuration files.
        /// </summary>
        private IReadOnlyDictionary<string, IFile> FindHostTemplateConfigFiles()
        {
            Dictionary<string, IFile> allHostFilesForTemplate = new();

            foreach (IFile hostFile in ConfigDirectory.EnumerateFiles($"*{HostTemplateFileConfigBaseName}", SearchOption.TopDirectoryOnly))
            {
                allHostFilesForTemplate.Add(hostFile.Name.Replace(HostTemplateFileConfigBaseName, string.Empty), hostFile);
                Logger.LogDebug($"Found *{HostTemplateFileConfigBaseName} at {hostFile.GetDisplayPath()}.");
            }

            return allHostFilesForTemplate;
        }

        private IReadOnlyDictionary<CultureInfo, TemplateLocalization> FindLocalizations()
        {
            Dictionary<CultureInfo, TemplateLocalization> localizations = new();
            IDirectory? localizeFolder = ConfigDirectory.DirectoryInfo("localize");
            if (localizeFolder == null || !localizeFolder.Exists)
            {
                return localizations;
            }

            foreach (IFile locFile in localizeFolder.EnumerateFiles(LocalizationFilePrefix + "*" + LocalizationFileExtension, SearchOption.AllDirectories))
            {
                string localeStr = locFile.Name.Substring(LocalizationFilePrefix.Length, locFile.Name.Length - LocalizationFilePrefix.Length - LocalizationFileExtension.Length);

                CultureInfo? locale = CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c => c.Name.Equals(localeStr, StringComparison.OrdinalIgnoreCase));
                if (locale == null)
                {
                    Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, locFile.GetDisplayPath());
                    Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_UnknownLocale, localeStr);
                    continue;
                }

                try
                {
                    LocalizationModel locModel = LocalizationModelDeserializer.Deserialize(locFile);
                    if (VerifyLocalizationModel(locModel, locFile))
                    {
                        localizations[locale] = new TemplateLocalization(locale, locModel, locFile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, locFile.GetDisplayPath());
                    Logger.LogDebug("Details: {0}", ex);
                }
            }
            return localizations;
        }

        private class TemplateLocalization : ILocalizationLocator
        {
            public TemplateLocalization(CultureInfo locale, LocalizationModel model, IFile file)
            {
                Locale = locale;
                Model = model;
                File = file;
            }

            string ILocalizationLocator.Locale => Locale.Name;

            string ILocalizationLocator.ConfigPlace => File.FullPath;

            string ILocalizationLocator.Identity => throw new NotImplementedException();

            string? ILocalizationLocator.Author => Model.Author;

            string? ILocalizationLocator.Name => Model.Name;

            string? ILocalizationLocator.Description => Model.Description;

            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ILocalizationLocator.ParameterSymbols => Model.ParameterSymbols;

            internal CultureInfo Locale { get; }

            internal LocalizationModel Model { get; }

            internal IFile File { get; }
        }
    }
}
