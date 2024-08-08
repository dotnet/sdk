// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// The class represents the template loaded from directory.
    /// The configuration can be loaded from file (production scenario) or loaded from <see cref="TemplateConfigModel"/> (test scenario).
    /// In both cases the template should be available from <see cref="TemplateSourceRoot"/>.
    /// </summary>
    internal abstract partial class DirectoryBasedTemplate
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        internal const string LocalizationFilePrefix = "templatestrings.";
        internal const string LocalizationFileExtension = ".json";
        protected const string AdditionalConfigFilesIndicator = "AdditionalConfigFiles";

        private readonly List<IValidationEntry> _validationErrors = new();

        /// <summary>
        /// Creates the instance of the class based on configuration from <paramref name="templateFile"/>.
        /// </summary>
        /// <exception cref="TemplateAuthoringException">when template configuration is invalid.</exception>
        /// <exception cref="InvalidOperationException">when template identity is null.</exception>
        /// <exception cref="NotSupportedException">when the template is not supported by current generator version.</exception>
        protected DirectoryBasedTemplate(IEngineEnvironmentSettings settings, IGenerator generator, IFile templateFile, string? baselineName = null)
        {
            EngineEnvironmentSettings = settings;
            //TODO: create specific logger if needed
            Logger = settings.Host.Logger;
            Generator = generator;

            ConfigFile = templateFile;

            if (ConfigFile.Parent?.Parent is null)
            {
                throw new TemplateAuthoringException(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource);
            }
            ConfigDirectory = templateFile.Parent;
            TemplateSourceRoot = ConfigFile.Parent.Parent;

            ConfigurationModel = TemplateConfigModel.FromJObject(
                MergeAdditionalConfiguration(templateFile.ReadJObjectFromIFile(), templateFile),
                Logger,
                baselineName);
            CheckGeneratorVersionRequiredByTemplate();
        }

        /// <summary>
        /// Test constructor. Do not use in production.
        /// This constructor does not set the location of configuration file.
        /// </summary>
        protected DirectoryBasedTemplate(IEngineEnvironmentSettings settings, IGenerator generator, TemplateConfigModel configModel, IDirectory templateSource)
        {
            EngineEnvironmentSettings = settings;
            //TODO: create specific logger if needed
            Logger = settings.Host.Logger;
            Generator = generator;

            TemplateSourceRoot = templateSource;

            ConfigurationModel = configModel;

            CheckGeneratorVersionRequiredByTemplate();
        }

        public ILogger Logger { get; }

        /// <summary>
        /// Gets the configuration model.
        /// For test purposes, preloaded model can be used.
        /// </summary>
        public TemplateConfigModel ConfigurationModel { get; }

        /// <summary>
        /// Gets the directory with source files for the template.
        /// </summary>
        public IDirectory TemplateSourceRoot { get; }

        public IEngineEnvironmentSettings EngineEnvironmentSettings { get; }

        /// <summary>
        /// Gets configuration <see cref="IFile"></see>. <see langword="null"/> when the template is created from the model built from code.
        /// </summary>
        public virtual IFile? ConfigFile { get; }

        /// <summary>
        /// Gets the configuration directory. <see langword="null"/> when the template is created from the model built from code.
        /// </summary>
        public virtual IDirectory? ConfigDirectory { get; }

        protected IGenerator Generator { get; }

        /// <summary>
        /// Gets the template parameters.
        /// </summary>
        protected IParameterDefinitionSet Parameters => new ParameterDefinitionSet(ConfigurationModel.ExtractParameters());

        internal Task ValidateAsync(ValidationScope scope, CancellationToken cancellationToken)
        {
            try
            {
                return ValidationManager.Instance.ValidateTemplateAsync(EngineEnvironmentSettings, this, scope, cancellationToken);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                //TODO: better error handling
                Logger.LogError("Failed to validate template: {ex}", ex.Message);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Parses host file name to get host identifier.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        protected string ParseHostFileName(string filename) => filename.Replace(HostTemplateFileConfigBaseName, string.Empty);

        /// <summary>
        /// Parses localization file name to get locale.
        /// </summary>
        /// <param name="locFile">localization file.</param>
        /// <returns></returns>
        protected CultureInfo? ParseLocFileName(IFile locFile)
        {
            string filename = locFile.Name;
            string localeStr = filename.Substring(LocalizationFilePrefix.Length, filename.Length - LocalizationFilePrefix.Length - LocalizationFileExtension.Length);
            CultureInfo? locale = null;

            try
            {
                // PERF: Avoid calling CultureInfo.GetCultures and searching the results as it heavily allocates on each invocation.
                locale = CultureInfo.GetCultureInfo(localeStr);
            }
            catch (CultureNotFoundException)
            {
                Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_UnknownLocale, localeStr);
            }
            return locale;
        }

        /// <summary>
        /// Checks the <paramref name="primarySource"/> for additional configuration files.
        /// If found, merges them all together.
        /// Returns the merged JObject (or the original if there was nothing to merge).
        /// Additional files must be in the same folder as the template file.
        /// </summary>
        /// <exception cref="TemplateAuthoringException">when additional files configuration is invalid.</exception>
        private static JObject MergeAdditionalConfiguration(JObject primarySource, IFileSystemInfo primarySourceConfig)
        {
            IReadOnlyList<string> otherFiles = primarySource.ArrayAsStrings(AdditionalConfigFilesIndicator);

            if (!otherFiles.Any())
            {
                return primarySource;
            }

            JObject combinedSource = (JObject)primarySource.DeepClone();

            foreach (string partialConfigFileName in otherFiles)
            {
                if (!partialConfigFileName.EndsWith("." + RunnableProjectGenerator.TemplateConfigFileName))
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.SimpleConfigModel_AuthoringException_MergeConfiguration_InvalidFileName, partialConfigFileName, RunnableProjectGenerator.TemplateConfigFileName), partialConfigFileName);
                }

                IFile? partialConfigFile = (primarySourceConfig.Parent?.EnumerateFiles(partialConfigFileName, SearchOption.TopDirectoryOnly).FirstOrDefault(x => string.Equals(x.Name, partialConfigFileName)))
                    ?? throw new TemplateAuthoringException(
                        string.Format(
                            LocalizableStrings.SimpleConfigModel_AuthoringException_MergeConfiguration_FileNotFound,
                            partialConfigFileName),
                        partialConfigFileName);
                JObject partialConfigJson = partialConfigFile.ReadJObjectFromIFile();
                combinedSource.Merge(partialConfigJson);
            }

            return combinedSource;
        }

        /// <summary>
        /// Checks if the template is supported by current generator version.
        /// </summary>
        /// <exception cref="NotSupportedException">when the template is not supported by current generator version.</exception>
        /// <exception cref="InvalidOperationException">when check for the version leads to unexpected result.</exception>
        private void CheckGeneratorVersionRequiredByTemplate()
        {
            if (string.IsNullOrWhiteSpace(ConfigurationModel.GeneratorVersions))
            {
                return;
            }

            string allowedGeneratorVersions = ConfigurationModel.GeneratorVersions!;

            if (!VersionStringHelpers.TryParseVersionSpecification(allowedGeneratorVersions, out IVersionSpecification? versionChecker))
            {
                throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateVersionNotSupported, allowedGeneratorVersions, RunnableProjectGenerator.GeneratorVersion));
            }

            if (versionChecker is null)
            {
                throw new InvalidOperationException($"{nameof(versionChecker)} cannot be null when {nameof(VersionStringHelpers.TryParseVersionSpecification)} is 'true'");
            }

            if (!versionChecker.CheckIfVersionIsValid(RunnableProjectGenerator.GeneratorVersion))
            {
                throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateVersionNotSupported, allowedGeneratorVersions, RunnableProjectGenerator.GeneratorVersion));
            }
        }
    }
}
