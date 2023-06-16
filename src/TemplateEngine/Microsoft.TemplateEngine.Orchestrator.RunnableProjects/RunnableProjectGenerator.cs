// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public sealed class RunnableProjectGenerator : IGenerator
    {
        internal const string TemplateConfigDirectoryName = ".template.config";
        internal const string TemplateConfigFileName = "template.json";

        internal const string GeneratorVersion = "1.0.0.0";
        private static readonly Guid GeneratorId = new("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        /// <inheritdoc/>
        Guid IIdentifiedComponent.Id => GeneratorId;

        /// <inheritdoc/>
        async Task<IReadOnlyList<IScanTemplateInfo>> IGenerator.GetTemplatesFromMountPointAsync(IMountPoint source, CancellationToken cancellationToken)
        {
            return await GetTemplatesFromMountPointInternalAsync(source, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async Task<ITemplate?> IGenerator.LoadTemplateAsync(IEngineEnvironmentSettings settings, ITemplateLocator templateLocator, string? baselineName, CancellationToken cancellationToken)
        {
            IMountPoint? mountPoint = null;
            IFile? configFile = null;
            try
            {
                if (!settings.TryGetMountPoint(templateLocator.MountPointUri, out mountPoint))
                {
                    return null;
                }
                if (mountPoint == null)
                {
                    throw new InvalidOperationException($"{nameof(mountPoint)} is null after {nameof(EngineEnvironmentSettingsExtensions.TryGetMountPoint)} returned true.");
                }
                configFile = mountPoint.FileInfo(templateLocator.ConfigPlace);
                if (configFile == null)
                {
                    return null;
                }
                IFile? localeConfig = null;
                IFile? hostTemplateConfigFile = null;
                if (templateLocator is IExtendedTemplateLocator extendedTemplateLocator)
                {
                    localeConfig = string.IsNullOrWhiteSpace(extendedTemplateLocator.LocaleConfigPlace) ? null : mountPoint.FileInfo(extendedTemplateLocator.LocaleConfigPlace!);
                    hostTemplateConfigFile = string.IsNullOrWhiteSpace(extendedTemplateLocator.HostConfigPlace) ? null : mountPoint.FileInfo(extendedTemplateLocator.HostConfigPlace!);
                }

                RunnableProjectConfig loadedTemplate = new RunnableProjectConfig(settings, this, configFile, hostTemplateConfigFile, localeConfig, baselineName);
                await loadedTemplate.ValidateAsync(ValidationScope.Instantiation, cancellationToken).ConfigureAwait(false);
                if (loadedTemplate.Localization != null && loadedTemplate.Localization.IsValid)
                {
                    loadedTemplate.Localize();
                }

                return loadedTemplate;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (NotSupportedException ex)
            {
                //do not print stack trace for this type.
                settings.Host.Logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, configFile?.GetDisplayPath(), ex.Message);
            }
            catch (TemplateAuthoringException ex)
            {
                //do not print stack trace for this type.
                settings.Host.Logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, configFile?.GetDisplayPath(), ex.Message);
            }
            catch (Exception ex)
            {
                //unexpected error - print details
                settings.Host.Logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, configFile?.GetDisplayPath(), ex);
            }
            return null;
        }

        /// <summary>
        /// Converts the raw, string version of a parameter to a strongly typed value. If the parameter has a datatype specified, use that. Otherwise attempt to infer the type.
        /// Throws a TemplateParamException if the conversion fails for any reason.
        /// </summary>
        object? IGenerator.ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
        {
            valueResolutionError = !ParameterConverter.TryConvertParameterValueToType(parameter, untypedValue, out object? convertedValue);
            return convertedValue;
        }

        /// <inheritdoc/>
        bool IGenerator.TryEvaluateFromString(ILogger logger, string text, IDictionary<string, object> variables, out bool result, out string evaluationError, HashSet<string>? referencedVariablesKeys)
        {
            VariableCollection variableCollection = new(null, variables);
            result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(logger, text, variableCollection, out evaluationError!, referencedVariablesKeys);
            return string.IsNullOrEmpty(evaluationError);
        }

        /// <inheritdoc/>
        async Task<ICreationEffects> IGenerator.GetCreationEffectsAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSetData parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (templateData is not IRunnableProjectConfig templateConfig)
            {
                throw new InvalidOperationException($"Load template using {nameof(IGenerator.LoadTemplateAsync)} to use this method.");
            }
            if (templateData.TemplateSourceRoot is null)
            {
                throw new InvalidOperationException($"{nameof(templateData.TemplateSourceRoot)} cannot be null to continue.");
            }

            RemoveDisabledParameters(parameters, templateConfig);
            IVariableCollection variables = SetupVariables(parameters, templateConfig.GlobalOperationConfig.VariableSetup);
            await templateConfig.EvaluateBindSymbolsAsync(environmentSettings, variables, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IMacroConfig> sortedMacroConfigs = MacroProcessor.SortMacroConfigsByDependencies(templateConfig.GlobalOperationConfig.SymbolNames, templateConfig.GlobalOperationConfig.Macros);
            MacroProcessor.ProcessMacros(environmentSettings, sortedMacroConfigs, variables);
            templateConfig.Evaluate(variables);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(environmentSettings.Host.Logger, environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateData.TemplateSourceRoot, environmentSettings.Components, variables, templateConfig);
            List<IFileChange2> changes = new List<IFileChange2>();

            foreach (FileSourceMatchInfo source in templateConfig.EvaluatedSources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                IDirectory? sourceDirectory =
                    templateData.TemplateSourceRoot.DirectoryInfo(source.Source) ??
                    throw new InvalidOperationException($"Cannot access the source directory of the template: {source.Source}.");
                IReadOnlyList<IFileChange2> fileChanges = orchestrator.GetFileChanges(runSpec, sourceDirectory, target);

                //source and target paths in the file changes are returned relative to source passed
                //GetCreationEffects method should return the source paths relative to template source root (location of .template.config folder) and target paths relative to output path and not relative to certain source
                //add source and target used to file changes to be returned as the result
                changes.AddRange(
                    fileChanges.Select(
                        fileChange => new FileChange(
                            Path.Combine(source.Source, fileChange.SourceRelativePath),
                            Path.Combine(source.Target, fileChange.TargetRelativePath),
                            fileChange.ChangeKind,
#pragma warning disable CS0618 // Type or member is obsolete
                            fileChange.Contents)));
#pragma warning restore CS0618 // Type or member is obsolete
            }

            return new CreationEffects2(changes, GetCreationResult(templateConfig));
        }

        /// <inheritdoc/>
        Task<ICreationResult> IGenerator.CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSetData parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (templateData is not RunnableProjectConfig templateConfig)
            {
                throw new InvalidOperationException($"Load template using {nameof(IGenerator.LoadTemplateAsync)} to use this method.");
            }
            if (templateData.TemplateSourceRoot is null)
            {
                throw new InvalidOperationException($"{nameof(templateData.TemplateSourceRoot)} cannot be null to continue.");
            }

            return CreateAsync(
                environmentSettings,
                templateConfig,
                templateData.TemplateSourceRoot,
                parameters,
                targetDirectory,
                cancellationToken);
        }

        #region Obsolete members

        [Obsolete("Replaced by CreateAsync with IEvaluatedParameterSetData", false)]
        Task<ICreationResult> IGenerator.CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate template,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            return ((IGenerator)this).CreateAsync(environmentSettings, template, parameters.ToParameterSetData(), targetDirectory, cancellationToken);
        }

        [Obsolete("Replaced by GetCreationEffectsAsync with IEvaluatedParameterSetData", false)]
        Task<ICreationEffects> IGenerator.GetCreationEffectsAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate template,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            return ((IGenerator)this).GetCreationEffectsAsync(environmentSettings, template, parameters.ToParameterSetData(), targetDirectory, cancellationToken);
        }

        [Obsolete("Replaced by ParameterSetBuilder.CreateWithDefaults", true)]
        IParameterSet IGenerator.GetParametersForTemplate(IEngineEnvironmentSettings environmentSettings, ITemplate template)
        {
            throw new NotImplementedException("Replaced by ParameterSetBuilder.CreateWithDefaults");
        }

        /// <summary>
        /// Scans the <paramref name="source"/> for the available templates.
        /// </summary>
        /// <param name="source">the mount point to scan for the templates.</param>
        /// <param name="localizations">found localization definitions.</param>
        /// <returns>the list of found templates and list of found localizations via <paramref name="localizations"/>.</returns>
        [Obsolete]
        IList<ITemplate> IGenerator.GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations)
        {
            IReadOnlyList<ScannedTemplateInfo> foundTemplates =
                      Task.Run(async () => await GetTemplatesFromMountPointInternalAsync(source, default).ConfigureAwait(false)).GetAwaiter().GetResult();

            localizations = foundTemplates.SelectMany(t => t.Localizations.Values).OfType<ILocalizationLocator>().ToList();

            return foundTemplates.Select(t => (ITemplate)new LegacyTemplate(t, this)).ToList();
        }

        /// <summary>
        /// Attempts to load template configuration from <paramref name="templateFileConfig"/>.
        /// </summary>
        /// <param name="templateFileConfig">the template configuration entry, should be a file.</param>
        /// <param name="template">loaded template.</param>
        /// <param name="localeFileConfig">the template localization configuration entry, should be a file.</param>
        /// <param name="hostTemplateConfigFile">the template host configuration entry, should be a file.</param>
        /// <param name="baselineName">the baseline to load.</param>
        /// <returns>true when template can be loaded, false otherwise. The loaded template is returned in <paramref name="template"/>.</returns>
        [Obsolete]
        bool IGenerator.TryGetTemplateFromConfigInfo(IFileSystemInfo templateFileConfig, out ITemplate? template, IFileSystemInfo? localeFileConfig, IFile? hostTemplateConfigFile, string? baselineName)
        {
            _ = templateFileConfig ?? throw new ArgumentNullException(nameof(templateFileConfig));
            ILogger logger = templateFileConfig.MountPoint.EnvironmentSettings.Host.LoggerFactory.CreateLogger<RunnableProjectGenerator>();
            try
            {
                IFile templateFile = templateFileConfig as IFile
                    ?? throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_ConfigShouldBeFile, templateFileConfig.GetDisplayPath()));

                IFile? localeFile = null;
                if (localeFileConfig != null)
                {
                    localeFile = localeFileConfig as IFile
                      ?? throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_LocaleConfigShouldBeFile, localeFileConfig.GetDisplayPath()));
                }

                RunnableProjectConfig loadedTemplate = new RunnableProjectConfig(templateFileConfig.MountPoint.EnvironmentSettings, this, templateFile, hostTemplateConfigFile, localeFile, baselineName);
                Task.Run(async () => await loadedTemplate.ValidateAsync(ValidationScope.Instantiation, CancellationToken.None).ConfigureAwait(false)).GetAwaiter().GetResult();
                template = loadedTemplate;
                return true;
            }
            catch (NotSupportedException ex)
            {
                //do not print stack trace for this type.
                logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, templateFileConfig.GetDisplayPath(), ex.Message);
            }
            catch (TemplateAuthoringException ex)
            {
                //do not print stack trace for this type.
                logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, templateFileConfig.GetDisplayPath(), ex.Message);
            }
            catch (Exception ex)
            {
                //unexpected error - print details
                logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, templateFileConfig.GetDisplayPath(), ex);
            }
            template = null;
            return false;
        }

        #endregion

        internal static async Task<ICreationResult> CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            IRunnableProjectConfig runnableProjectConfig,
            IDirectory templateSourceRoot,
            IParameterSetData parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RemoveDisabledParameters(parameters, runnableProjectConfig);
            IVariableCollection variables = SetupVariables(parameters, runnableProjectConfig.GlobalOperationConfig.VariableSetup);
            await runnableProjectConfig.EvaluateBindSymbolsAsync(environmentSettings, variables, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IMacroConfig> sortedMacroConfigs = MacroProcessor.SortMacroConfigsByDependencies(runnableProjectConfig.GlobalOperationConfig.SymbolNames, runnableProjectConfig.GlobalOperationConfig.Macros);
            MacroProcessor.ProcessMacros(environmentSettings, sortedMacroConfigs, variables);
            runnableProjectConfig.Evaluate(variables);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(environmentSettings.Host.Logger, environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateSourceRoot, environmentSettings.Components, variables, runnableProjectConfig);

            foreach (FileSourceMatchInfo source in runnableProjectConfig.EvaluatedSources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                IDirectory? sourceDirectory =
                    templateSourceRoot.DirectoryInfo(source.Source) ??
                    throw new InvalidOperationException($"Cannot access the source directory of the template: {source.Source}.");
                orchestrator.Run(runSpec, sourceDirectory, target);
            }

            return GetCreationResult(runnableProjectConfig);
        }

        internal async Task<IReadOnlyList<ScannedTemplateInfo>> GetTemplatesFromMountPointInternalAsync(IMountPoint source, CancellationToken cancellationToken)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));
            ILogger logger = source.EnvironmentSettings.Host.LoggerFactory.CreateLogger<RunnableProjectGenerator>();
            IDirectory folder = source.Root;
            List<ScannedTemplateInfo> templateList = new();

            foreach (IFile file in folder.EnumerateFiles(TemplateConfigFileName, SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogDebug($"Found {TemplateConfigFileName} at {file.GetDisplayPath()}.");
                try
                {
                    var discoveredTemplate = new ScannedTemplateInfo(source.EnvironmentSettings, this, file);
                    await discoveredTemplate.ValidateAsync(ValidationScope.Scanning, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    templateList.Add(discoveredTemplate);
                }
                catch (NotSupportedException ex)
                {
                    //do not print stack trace for this type.
                    logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, file.GetDisplayPath(), ex.Message);
                }
                catch (TemplateAuthoringException ex)
                {
                    //do not print stack trace for this type.
                    logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, file.GetDisplayPath(), ex.Message);
                }
                catch (Exception ex)
                {
                    //unexpected error - print details
                    logger.LogError(LocalizableStrings.Authoring_TemplateNotInstalled_Message, file.GetDisplayPath(), ex);
                }
            }
            return templateList;
        }

        private static void RemoveDisabledParameters(
            IParameterSetData parameters,
            IRunnableProjectConfig runnableProjectConfig)
        {
            parameters.Values
                .Where(v => !v.IsEnabled)
                .ForEach(p => runnableProjectConfig.RemoveParameter(p.ParameterDefinition));
        }

        private static IVariableCollection SetupVariables(IParameterSetData parameters, IVariableConfig variableConfig)
        {
            IVariableCollection variables = ParameterBasedVariableCollection.SetupParameterBasedVariables(parameters, variableConfig);

            foreach (Parameter param in parameters.ParametersDefinition.OfType<Parameter>())
            {
                // Add choice values to variables - to allow them to be recognizable unquoted
                if (param.EnableQuotelessLiterals && param.IsChoice() && param.Choices != null)
                {
                    foreach (string choiceKey in param.Choices.Keys)
                    {
                        if (
                            variables.TryGetValue(choiceKey, out object? existingValueObj) &&
                            existingValueObj is string existingValue &&
                            !string.Equals(choiceKey, existingValue, StringComparison.CurrentCulture))
                        {
                            throw new InvalidOperationException(string.Format(LocalizableStrings.RunnableProjectGenerator_CannotAddImplicitChoice, choiceKey, existingValue));
                        }
                        variables[choiceKey] = choiceKey;
                    }
                }
            }

            return variables;
        }

        private static ICreationResult GetCreationResult(IRunnableProjectConfig runnableProjectConfig)
        {
            return new CreationResult(runnableProjectConfig.PostActions, runnableProjectConfig.PrimaryOutputs);
        }

        /// <summary>
        /// Wrapper class: ScannedTemplateInfo -> ITemplate.
        /// </summary>
        [Obsolete]
        private class LegacyTemplate : ITemplate
        {
            private readonly ScannedTemplateInfo _templateInfo;
            private readonly IGenerator _generator;

            internal LegacyTemplate(ScannedTemplateInfo templateInfo, IGenerator generator)
            {
                _templateInfo = templateInfo;
                _generator = generator;
            }

            public IGenerator Generator => _generator;

            public IFileSystemInfo Configuration => _templateInfo.ConfigFile;

            public IFileSystemInfo? LocaleConfiguration => null;

            public IFileSystemInfo? HostSpecificConfiguration => null;

            public IDirectory TemplateSourceRoot => _templateInfo.TemplateSourceRoot;

            public bool IsNameAgreementWithFolderPreferred => _templateInfo.ConfigurationModel.PreferNameDirectory;

            public bool PreferDefaultName => _templateInfo.ConfigurationModel.PreferDefaultName;

            public string? Author => _templateInfo.ConfigurationModel.Author;

            public string? Description => _templateInfo.ConfigurationModel.Description;

            public IReadOnlyList<string> Classifications => _templateInfo.ConfigurationModel.Classifications;

            public string? DefaultName => _templateInfo.ConfigurationModel.DefaultName;

            public string Identity => _templateInfo.ConfigurationModel.Identity;

            public Guid GeneratorId => _generator.Id;

            public string? GroupIdentity => _templateInfo.ConfigurationModel.GroupIdentity;

            public int Precedence => _templateInfo.ConfigurationModel.Precedence;

            public string Name => _templateInfo.ConfigurationModel.Name ?? throw new TemplateAuthoringException("Template configuration should have 'name' defined.", "name");

            public IReadOnlyDictionary<string, string> TagsCollection => _templateInfo.ConfigurationModel.Tags;

            public IParameterDefinitionSet ParameterDefinitions => new ParameterDefinitionSet(_templateInfo.ConfigurationModel.ExtractParameters());

            public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

            public string MountPointUri => _templateInfo.ConfigFile.MountPoint.MountPointUri;

            public string ConfigPlace => _templateInfo.ConfigFile.FullPath;

            public string? LocaleConfigPlace => throw new NotImplementedException();

            public string? HostConfigPlace => throw new NotImplementedException();

            public string? ThirdPartyNotices => _templateInfo.ConfigurationModel.ThirdPartyNotices;

            public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _templateInfo.ConfigurationModel.BaselineInfo;

            public IReadOnlyList<string> ShortNameList => _templateInfo.ConfigurationModel.ShortNameList;

            public IReadOnlyList<Guid> PostActions => _templateInfo.ConfigurationModel.PostActionModels.Select(pam => pam.ActionId).ToArray();

            public IReadOnlyList<TemplateConstraintInfo> Constraints => _templateInfo.ConfigurationModel.Constraints;

            public bool IsValid => !ValidationErrors.Any(e => e.Severity == IValidationEntry.SeverityLevel.Error);

            public IReadOnlyList<IValidationEntry> ValidationErrors => _templateInfo.ValidationErrors;

            public ILocalizationLocator? Localization => throw new NotImplementedException();

            #region Obsolete members

            [Obsolete]
            string ITemplateInfo.ShortName
            {
                get
                {
                    if (((ITemplateInfo)this).ShortNameList.Count > 0)
                    {
                        return ((ITemplateInfo)this).ShortNameList[0];
                    }

                    return string.Empty;
                }
            }

            [Obsolete]
            IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags
            {
                get
                {
                    Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
                    foreach (KeyValuePair<string, string> tag in ((ITemplateInfo)this).TagsCollection)
                    {
                        tags[tag.Key] = new CacheTag(null, null, new Dictionary<string, ParameterChoice> { { tag.Value, new ParameterChoice(null, null) } }, tag.Value);
                    }
                    foreach (ITemplateParameter parameter in ((ITemplateInfo)this).ParameterDefinitions.Where(TemplateParameterExtensions.IsChoice))
                    {
                        IReadOnlyDictionary<string, ParameterChoice> choices = parameter.Choices ?? new Dictionary<string, ParameterChoice>();
                        tags[parameter.Name] = new CacheTag(parameter.DisplayName, parameter.Description, choices, parameter.DefaultValue);
                    }
                    return tags;
                }
            }

            [Obsolete]
            IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters
            {
                get
                {
                    Dictionary<string, ICacheParameter> cacheParameters = new Dictionary<string, ICacheParameter>();
                    foreach (ITemplateParameter parameter in ((ITemplateInfo)this).ParameterDefinitions.Where(TemplateParameterExtensions.IsChoice))
                    {
                        cacheParameters[parameter.Name] = new CacheParameter()
                        {
                            DataType = parameter.DataType,
                            DefaultValue = parameter.DefaultValue,
                            Description = parameter.Documentation,
                            DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue,
                            DisplayName = parameter.DisplayName

                        };
                    }
                    return cacheParameters;
                }
            }

            [Obsolete("Use ParameterDefinitionSet instead.")]
            IReadOnlyList<ITemplateParameter> ITemplateInfo.Parameters => ParameterDefinitions;

            [Obsolete]
            bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

            #endregion

            public void Dispose()
            {
            }
        }
    }
}
