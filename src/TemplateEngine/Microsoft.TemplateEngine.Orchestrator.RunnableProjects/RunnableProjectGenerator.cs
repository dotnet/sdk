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
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public sealed class RunnableProjectGenerator : IGenerator
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        internal const string TemplateConfigDirectoryName = ".template.config";
        internal const string TemplateConfigFileName = "template.json";
        internal const string LocalizationFilePrefix = "templatestrings.";
        internal const string LocalizationFileExtension = ".json";
        internal const string GeneratorVersion = "1.0.0.0";
        private static readonly Guid GeneratorId = new("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        Guid IIdentifiedComponent.Id => GeneratorId;

        /// <summary>
        /// Converts the raw, string version of a parameter to a strongly typed value. If the parameter has a datatype specified, use that. Otherwise attempt to infer the type.
        /// Throws a TemplateParamException if the conversion fails for any reason.
        /// </summary>
        object? IGenerator.ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
        {
            return ParameterConverter.ConvertParameterValueToType(environmentSettings.Host, parameter, untypedValue, out valueResolutionError);
        }

        bool IGenerator.TryEvaluateFromString(ILogger logger, string text, IDictionary<string, object> variables, out bool result, out string evaluationError, HashSet<string>? referencedVariablesKeys)
        {
            VariableCollection variableCollection = new VariableCollection(null, variables);
            result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(logger, text, variableCollection, out evaluationError, referencedVariablesKeys);
            return string.IsNullOrEmpty(evaluationError);
        }

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

        Task<ICreationResult> IGenerator.CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSetData parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            RunnableProjectConfig templateConfig = (RunnableProjectConfig)templateData;

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

        /// <summary>
        /// Performs the dry-run of the template instantiation to evaluate the primary outputs, post actions to be applied and file changes to be made when executing the template with specified parameters.
        /// </summary>
        /// <param name="environmentSettings">environment settings.</param>
        /// <param name="templateData">the template to be executed.</param>
        /// <param name="parameters">the parameters to be used on template execution.</param>
        /// <param name="targetDirectory">the output path for the template.</param>
        /// <param name="cancellationToken">cancellationToken.</param>
        /// <returns>the primary outputs, post actions and file changes that will be made when executing the template with specified parameters.</returns>
        async Task<ICreationEffects> IGenerator.GetCreationEffectsAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSetData parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunnableProjectConfig templateConfig = (RunnableProjectConfig)templateData;
            if (templateData.TemplateSourceRoot is null)
            {
                throw new InvalidOperationException($"{nameof(templateData.TemplateSourceRoot)} cannot be null to continue.");
            }

            IVariableCollection variables = SetupVariables(parameters, templateConfig.GlobalOperationConfig.VariableSetup);
            await templateConfig.EvaluateBindSymbolsAsync(environmentSettings, variables, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ProcessMacros(environmentSettings, templateConfig.GlobalOperationConfig, variables);
            templateConfig.Evaluate(variables);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(environmentSettings.Host.Logger, environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateData.TemplateSourceRoot, environmentSettings.Components, variables, templateConfig);
            List<IFileChange2> changes = new List<IFileChange2>();

            foreach (FileSourceMatchInfo source in templateConfig.EvaluatedSources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                IDirectory? sourceDirectory = templateData.TemplateSourceRoot.DirectoryInfo(source.Source);
                if (sourceDirectory == null)
                {
                    throw new InvalidOperationException($"Cannot access the source directory of the template: {source.Source}.");
                }

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
        IList<ITemplate> IGenerator.GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));

            ILogger logger = source.EnvironmentSettings.Host.LoggerFactory.CreateLogger<RunnableProjectGenerator>();

            IDirectory folder = source.Root;
            IList<ITemplate> templateList = new List<ITemplate>();
            localizations = new List<ILocalizationLocator>();

            foreach (IFile file in folder.EnumerateFiles(TemplateConfigFileName, SearchOption.AllDirectories))
            {
                logger.LogDebug($"Found {TemplateConfigFileName} at {file.GetDisplayPath()}.");
                try
                {
                    IFile? hostConfigFile = FindBestHostTemplateConfigFile(source.EnvironmentSettings, file);
                    logger.LogDebug($"Found *{HostTemplateFileConfigBaseName} at {hostConfigFile?.GetDisplayPath()}.");

                    // issue here: we need to pass locale as parameter
                    // consider passing current locale file here if exists
                    // tracking issue: https://github.com/dotnet/templating/issues/3255
                    var templateConfiguration = new RunnableProjectConfig(source.EnvironmentSettings, this, file, hostConfigFile);

                    IDirectory? localizeFolder = file.Parent?.DirectoryInfo("localize");
                    if (localizeFolder != null && localizeFolder.Exists)
                    {
                        foreach (IFile locFile in localizeFolder.EnumerateFiles(LocalizationFilePrefix + "*" + LocalizationFileExtension, SearchOption.AllDirectories))
                        {
                            string locale = locFile.Name.Substring(LocalizationFilePrefix.Length, locFile.Name.Length - LocalizationFilePrefix.Length - LocalizationFileExtension.Length);

                            try
                            {
                                LocalizationModel locModel = LocalizationModelDeserializer.Deserialize(locFile);
                                if (templateConfiguration.VerifyLocalizationModel(locModel, locFile))
                                {
                                    localizations.Add(new LocalizationLocator(
                                        locale,
                                        locFile.FullPath,
                                        templateConfiguration.ConfigurationModel.Identity,
                                        locModel.Author ?? string.Empty,
                                        locModel.Name ?? string.Empty,
                                        locModel.Description ?? string.Empty,
                                        locModel.ParameterSymbols));
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, locFile.GetDisplayPath());
                                logger.LogDebug("Details: {0}", ex);
                            }
                        }
                    }
                    templateList.Add(templateConfiguration);

                }
                catch (TemplateValidationException)
                {
                    //do nothing
                    //template validation prints all required information
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

        /// <summary>
        /// Attempts to load template configuration from <paramref name="templateFileConfig"/>.
        /// </summary>
        /// <param name="templateFileConfig">the template configuration entry, should be a file.</param>
        /// <param name="template">loaded template.</param>
        /// <param name="localeFileConfig">the template localization configuration entry, should be a file.</param>
        /// <param name="hostTemplateConfigFile">the template host configuration entry, should be a file.</param>
        /// <param name="baselineName">the baseline to load.</param>
        /// <returns>true when template can be loaded, false otherwise. The loaded template is returned in <paramref name="template"/>.</returns>
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

                var templateConfiguration = new RunnableProjectConfig(templateFileConfig.MountPoint.EnvironmentSettings, this, templateFile, hostTemplateConfigFile, localeFile, baselineName);
                template = templateConfiguration;
                return true;
            }
            catch (TemplateValidationException)
            {
                //do nothing
                //template validation prints all required information
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

        internal async Task<ICreationResult> CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            IRunnableProjectConfig runnableProjectConfig,
            IDirectory templateSourceRoot,
            IParameterSetData parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IVariableCollection variables = SetupVariables(parameters, runnableProjectConfig.GlobalOperationConfig.VariableSetup);
            await runnableProjectConfig.EvaluateBindSymbolsAsync(environmentSettings, variables, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            ProcessMacros(environmentSettings, runnableProjectConfig.GlobalOperationConfig, variables);
            runnableProjectConfig.Evaluate(variables);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(environmentSettings.Host.Logger, environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateSourceRoot, environmentSettings.Components, variables, runnableProjectConfig);

            foreach (FileSourceMatchInfo source in runnableProjectConfig.EvaluatedSources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                IDirectory? sourceDirectory = templateSourceRoot.DirectoryInfo(source.Source);
                if (sourceDirectory == null)
                {
                    throw new InvalidOperationException($"Cannot access the source directory of the template: {source.Source}.");
                }

                orchestrator.Run(runSpec, sourceDirectory, target);
            }

            return GetCreationResult(runnableProjectConfig);
        }

        private static IVariableCollection SetupVariables(IParameterSetData parameters, IVariableConfig variableConfig)
        {
            IVariableCollection variables = VariableCollection.SetupVariables(parameters, variableConfig);

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

        // Note the deferred-config macros (generated) are part of the runConfig.Macros
        //      and not in the ComputedMacros.
        //  Possibly make a separate property for the deferred-config macros
        private static void ProcessMacros(IEngineEnvironmentSettings environmentSettings, IGlobalRunConfig runConfig, IVariableCollection variableCollection)
        {
            MacrosOperationConfig? macroProcessor = null;
            if (runConfig.Macros != null)
            {
                macroProcessor = new MacrosOperationConfig();
                macroProcessor.ProcessMacros(environmentSettings, runConfig.Macros, variableCollection);
            }

            if (runConfig.ComputedMacros != null)
            {
                macroProcessor ??= new MacrosOperationConfig();
                macroProcessor.ProcessMacros(environmentSettings, runConfig.ComputedMacros, variableCollection);
            }
        }

        private static ICreationResult GetCreationResult(IRunnableProjectConfig runnableProjectConfig)
        {
            return new CreationResult(runnableProjectConfig.PostActions, runnableProjectConfig.PrimaryOutputs);
        }

        private IFile? FindBestHostTemplateConfigFile(IEngineEnvironmentSettings engineEnvironment, IFile config)
        {
            IDictionary<string, IFile> allHostFilesForTemplate = new Dictionary<string, IFile>();

            if (config.Parent is null)
            {
                return null;
            }

            foreach (IFile hostFile in config.Parent.EnumerateFiles($"*{HostTemplateFileConfigBaseName}", SearchOption.TopDirectoryOnly))
            {
                allHostFilesForTemplate.Add(hostFile.Name, hostFile);
            }

            string preferredHostFileName = string.Concat(engineEnvironment.Host.HostIdentifier, HostTemplateFileConfigBaseName);
            if (allHostFilesForTemplate.TryGetValue(preferredHostFileName, out IFile preferredHostFile))
            {
                return preferredHostFile;
            }

            foreach (string fallbackHostName in engineEnvironment.Host.FallbackHostTemplateConfigNames)
            {
                string fallbackHostFileName = string.Concat(fallbackHostName, HostTemplateFileConfigBaseName);

                if (allHostFilesForTemplate.TryGetValue(fallbackHostFileName, out IFile fallbackHostFile))
                {
                    return fallbackHostFile;
                }
            }

            return null;
        }
    }
}
