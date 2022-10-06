// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
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
        protected const string AdditionalConfigFilesIndicator = "AdditionalConfigFiles";

        /// <summary>
        /// Creates the instance of the class based on configuration from <paramref name="templateFile"/>.
        /// </summary>
        /// <exception cref="TemplateValidationException">when template configuration is invalid.</exception>
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
                throw new TemplateValidationException(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource);
            }
            ConfigDirectory = templateFile.Parent;
            TemplateSourceRoot = ConfigFile.Parent.Parent;

            ConfigurationModel = TemplateConfigModel.FromJObject(
                MergeAdditionalConfiguration(templateFile.ReadJObjectFromIFile(), templateFile),
                Logger,
                baselineName,
                filename: templateFile.GetDisplayPath());

            CheckGeneratorVersionRequiredByTemplate();
            PerformTemplateValidation();
            TemplateIdentity = ConfigurationModel.Identity ?? throw new InvalidOperationException("Template identity cannot be null");
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
            PerformTemplateValidation();
            TemplateIdentity = ConfigurationModel.Identity!;
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

        protected string TemplateIdentity { get; }

        /// <summary>
        /// Gets the template parameters.
        /// </summary>
        protected IParameterDefinitionSet Parameters => new ParameterDefinitionSet(ConfigurationModel.ExtractParameters());

        /// <summary>
        /// Verifies that the given localization model was correctly constructed
        /// to localize this SimpleConfigModel.
        /// </summary>
        /// <param name="locModel">The localization model to be verified.</param>
        /// <param name="localeFile">localization file (optional), needed to get file name for logging.</param>
        /// <returns>True if the verification succeeds. False otherwise.
        /// Check logs for details in case of a failed verification.</returns>
        protected internal bool VerifyLocalizationModel(LocalizationModel locModel, IFile localeFile)
        {
            bool validModel = true;
            List<string> errorMessages = new List<string>();
            int unusedPostActionLocs = locModel.PostActions.Count;
            foreach (var postAction in ConfigurationModel.PostActionModels)
            {
                if (postAction.Id == null || !locModel.PostActions.TryGetValue(postAction.Id, out PostActionLocalizationModel postActionLocModel))
                {
                    // Post action with no localization model.
                    continue;
                }

                unusedPostActionLocs--;

                // Validate manual instructions.
                bool instructionUsesDefaultKey = postAction.ManualInstructionInfo.Count == 1 && postAction.ManualInstructionInfo[0].Id == null &&
                    postActionLocModel.Instructions.ContainsKey(PostActionModel.DefaultIdForSingleManualInstruction);
                if (instructionUsesDefaultKey)
                {
                    // Just one manual instruction using the default key. No issues. Continue.
                    continue;
                }

                int unusedManualInstructionLocs = postActionLocModel.Instructions.Count;
                foreach (var instruction in postAction.ManualInstructionInfo)
                {
                    if (instruction.Id != null && postActionLocModel.Instructions.ContainsKey(instruction.Id))
                    {
                        unusedManualInstructionLocs--;
                    }
                }

                if (unusedManualInstructionLocs > 0)
                {
                    // Localizations provide more translations than the number of manual instructions we have.
                    string excessInstructionLocalizationIds = string.Join(
                        ", ",
                        postActionLocModel.Instructions.Keys.Where(k => !postAction.ManualInstructionInfo.Any(i => i.Id == k)));

                    errorMessages.Add(string.Format(LocalizableStrings.Authoring_InvalidManualInstructionLocalizationIndex, excessInstructionLocalizationIds, postAction.Id));
                    validModel = false;
                }
            }

            if (unusedPostActionLocs > 0)
            {
                // Localizations provide more translations than the number of post actions we have.
                string excessPostActionLocalizationIds = string.Join(", ", locModel.PostActions.Keys.Where(k => !ConfigurationModel.PostActionModels.Any(p => p.Id == k)).Select(k => k.ToString()));
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_InvalidPostActionLocalizationIndex, excessPostActionLocalizationIds));
                validModel = false;
            }

            if (errorMessages.Any())
            {
                Logger.LogDebug($"Localization file {localeFile?.GetDisplayPath()} is not compatible with base configuration {ConfigFile?.GetDisplayPath()}");
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.RunnableProjectGenerator_Warning_LocFileSkipped, localeFile?.GetDisplayPath(), ConfigFile?.GetDisplayPath()));
                foreach (string errorMessage in errorMessages)
                {
                    stringBuilder.AppendLine("  " + errorMessage);
                }
                Logger.LogWarning(stringBuilder.ToString());
            }
            return validModel;
        }

        /// <summary>
        /// The method checks if all the sources are valid.
        /// Example: the source directory should exist, should not be a file, should be accessible via mount point.
        /// </summary>
        /// <returns>list of found errors.</returns>
        protected internal IEnumerable<string> ValidateTemplateSourcePaths()
        {
            List<string> errors = new();
            // check if any sources get out of the mount point
            foreach (ExtendedFileSource source in ConfigurationModel.Sources)
            {
                try
                {
                    IFile? file = TemplateSourceRoot.FileInfo(source.Source);
                    //template source root should not be a file
                    if (file?.Exists ?? false)
                    {
                        errors.Add(string.Format(LocalizableStrings.Authoring_SourceMustBeDirectory, source.Source));
                    }
                    else
                    {
                        IDirectory? sourceRoot = TemplateSourceRoot.DirectoryInfo(source.Source);
                        if (sourceRoot is null)
                        {
                            errors.Add(string.Format(LocalizableStrings.Authoring_SourceIsOutsideInstallSource, source.Source));
                        }
                        else if (!sourceRoot.Exists)
                        {
                            errors.Add(string.Format(LocalizableStrings.Authoring_SourceDoesNotExist, source.Source));
                        }
                    }
                }
                catch
                {
                    // outside the mount point root
                    // TODO: after the null ref exception in DirectoryInfo is fixed, change how this check works.
                    errors.Add(string.Format(LocalizableStrings.Authoring_SourceIsOutsideInstallSource, source.Source));
                }
            }
            return errors;
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

        /// <summary>
        /// The method validates loaded cpnfiguration. The errors and warnings are printed using logger.
        /// The warning messages is mainly only for template authoring, and should be used for template validation, so for now they are logged in debug level only.
        /// https://github.com/dotnet/templating/issues/2623.
        /// </summary>
        /// <exception cref="TemplateValidationException">in case of validation fails.</exception>
        private void PerformTemplateValidation()
        {
            //Do some basic checks...
            List<string> errorMessages = new List<string>();
            List<string> warningMessages = new List<string>();

            #region Errors
            if (string.IsNullOrWhiteSpace(ConfigurationModel.Identity))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "identity"));
            }

            if (string.IsNullOrWhiteSpace(ConfigurationModel.Name))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "name"));
            }

            if ((ConfigurationModel.ShortNameList?.Count ?? 0) == 0)
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "shortName"));
            }

            var invalidMultichoices =
                ConfigurationModel.Symbols
                    .OfType<ParameterSymbol>()
                    .Where(p => p.AllowMultipleValues)
                    .Where(p => p.Choices.Any(c => !c.Key.IsValidMultiValueParameterValue()));
            errorMessages.AddRange(
                invalidMultichoices.Select(p =>
                    string.Format(
                        LocalizableStrings.Authoring_InvalidMultichoiceSymbol,
                        p.DisplayName,
                        string.Join(", ", MultiValueParameter.MultiValueSeparators.Select(c => $"'{c}'")),
                        string.Join(
                            ", ",
                            p.Choices.Where(c => !c.Key.IsValidMultiValueParameterValue())
                                .Select(c => $"{{{c.Key}}}")))));

            errorMessages.AddRange(ValidateTemplateSourcePaths());
            #endregion

            #region Warnings
            //TODO: the warning messages should be transferred to validate subcommand, as they are not useful for final user, but useful for template author.
            //https://github.com/dotnet/templating/issues/2623
            if (string.IsNullOrWhiteSpace(ConfigurationModel.SourceName))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "sourceName"));
            }

            if (string.IsNullOrWhiteSpace(ConfigurationModel.Author))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "author"));
            }

            if (string.IsNullOrWhiteSpace(ConfigurationModel.GroupIdentity))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "groupIdentity"));
            }

            if (string.IsNullOrWhiteSpace(ConfigurationModel.GeneratorVersions))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "generatorVersions"));
            }

            if (ConfigurationModel.Precedence == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "precedence"));
            }

            if ((ConfigurationModel.Classifications?.Count ?? 0) == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "classifications"));
            }

            if (ConfigurationModel.PostActionModels != null && ConfigurationModel.PostActionModels.Any(x => x.ManualInstructionInfo == null || x.ManualInstructionInfo.Count == 0))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MalformedPostActionManualInstructions));
            }
            #endregion

            if (warningMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateMissingCommonInformation, ConfigFile?.GetDisplayPath()));
                foreach (string message in warningMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                Logger.LogDebug(stringBuilder.ToString());
            }

            if (errorMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateNotInstalled, ConfigFile?.GetDisplayPath()));
                foreach (string message in errorMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                Logger.LogError(stringBuilder.ToString());
                throw new TemplateValidationException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateValidationFailed, ConfigFile?.GetDisplayPath()));
            }
        }
    }
}
