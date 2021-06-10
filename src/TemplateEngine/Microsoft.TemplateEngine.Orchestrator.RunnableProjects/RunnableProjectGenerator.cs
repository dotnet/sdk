// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectGenerator : IGenerator
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        internal const string TemplateConfigDirectoryName = ".template.config";
        internal const string TemplateConfigFileName = "template.json";
        internal const string LocalizationFilePrefix = "templatestrings.";
        internal const string LocalizationFileExtension = ".json";
        private const string GeneratorVersion = "1.0.0.0";
        private static readonly Guid GeneratorId = new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        public Guid Id => GeneratorId;

        /// <summary>
        /// Converts the raw, string version of a parameter to a strongly typed value. If the parameter has a datatype specified, use that. Otherwise attempt to infer the type.
        /// Throws a TemplateParamException if the conversion fails for any reason.
        /// </summary>
        public object? ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
        {
            return InternalConvertParameterValueToType(environmentSettings, parameter, untypedValue, out valueResolutionError);
        }

        public Task<ICreationResult> CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunnableProjectTemplate template = (RunnableProjectTemplate)templateData;
            ProcessMacros(environmentSettings, template.Config.OperationConfig, parameters);

            IVariableCollection variables = VariableCollection.SetupVariables(environmentSettings, parameters, template.Config.OperationConfig.VariableSetup);
            template.Config.Evaluate(parameters, variables);

            IOrchestrator2 basicOrchestrator = new Core.Util.Orchestrator();
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateData.TemplateSourceRoot, environmentSettings.Components, parameters, variables, template.Config.OperationConfig, template.Config.SpecialOperationConfig, template.Config.IgnoreFileNames);

            foreach (FileSourceMatchInfo source in template.Config.Sources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                orchestrator.Run(runSpec, templateData.TemplateSourceRoot.DirectoryInfo(source.Source), target);
            }

            return Task.FromResult(GetCreationResult(environmentSettings, template, variables));
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
        public Task<ICreationEffects> GetCreationEffectsAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            RunnableProjectTemplate template = (RunnableProjectTemplate)templateData;
            ProcessMacros(environmentSettings, template.Config.OperationConfig, parameters);

            IVariableCollection variables = VariableCollection.SetupVariables(environmentSettings, parameters, template.Config.OperationConfig.VariableSetup);
            template.Config.Evaluate(parameters, variables);

            IOrchestrator2 basicOrchestrator = new Core.Util.Orchestrator();
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateData.TemplateSourceRoot, environmentSettings.Components, parameters, variables, template.Config.OperationConfig, template.Config.SpecialOperationConfig, template.Config.IgnoreFileNames);
            List<IFileChange2> changes = new List<IFileChange2>();

            foreach (FileSourceMatchInfo source in template.Config.Sources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                IReadOnlyList<IFileChange2> fileChanges = orchestrator.GetFileChanges(runSpec, templateData.TemplateSourceRoot.DirectoryInfo(source.Source), target);

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

            return Task.FromResult((ICreationEffects)new CreationEffects2(changes, GetCreationResult(environmentSettings, template, variables)));
        }

        public IParameterSet GetParametersForTemplate(IEngineEnvironmentSettings environmentSettings, ITemplate template)
        {
            RunnableProjectTemplate tmplt = (RunnableProjectTemplate)template;
            return new ParameterSet(tmplt.Config);
        }

        /// <summary>
        /// Scans the <paramref name="source"/> for the available templates.
        /// </summary>
        /// <param name="source">the mount point to scan for the templates.</param>
        /// <param name="localizations">found localization definitions.</param>
        /// <returns>the list of found templates and list of found localizations via <paramref name="localizations"/>.</returns>
        public IList<ITemplate> GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));

            ILogger logger = source.EnvironmentSettings.Host.LoggerFactory.CreateLogger<RunnableProjectGenerator>();

            IDirectory folder = source.Root;
            IList<ITemplate> templateList = new List<ITemplate>();
            localizations = new List<ILocalizationLocator>();

            foreach (IFile file in folder.EnumerateFiles(TemplateConfigFileName, SearchOption.AllDirectories))
            {
                try
                {
                    IFile? hostConfigFile = FindBestHostTemplateConfigFile(source.EnvironmentSettings, file);
                    var templateModel = LoadBaseTemplate(file, logger: logger);

                    IDirectory localizeFolder = file.Parent.DirectoryInfo("localize");
                    if (localizeFolder != null && localizeFolder.Exists)
                    {
                        foreach (IFile locFile in localizeFolder.EnumerateFiles(LocalizationFilePrefix + "*" + LocalizationFileExtension, SearchOption.AllDirectories))
                        {
                            string locale = locFile.Name.Substring(LocalizationFilePrefix.Length, locFile.Name.Length - LocalizationFilePrefix.Length - LocalizationFileExtension.Length);

                            try
                            {
                                ILocalizationModel locModel = LocalizationModelDeserializer.Deserialize(locFile);
                                if (templateModel.VerifyLocalizationModel(locModel, out IEnumerable<string> errorMessages))
                                {
                                    localizations.Add(new LocalizationLocator(
                                        locale,
                                        locFile.FullPath,
                                        templateModel.Identity,
                                        locModel.Author ?? string.Empty,
                                        locModel.Name ?? string.Empty,
                                        locModel.Description ?? string.Empty,
                                        locModel.ParameterSymbols));
                                }
                                else
                                {
                                    HandleLocalizationValidationError(logger, file, locFile, errorMessages);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, locFile.GetDisplayPath());
                                logger.LogDebug("Details: {0}", ex);
                            }
                        }
                    }

                    // issue here: we need to pass locale as parameter
                    // consider passing current locale file here if exists
                    // tracking issue: https://github.com/dotnet/templating/issues/3255
                    RunnableProjectTemplate runnableProjectTemplate = new RunnableProjectTemplate(this, templateModel, null, hostConfigFile);
                    templateList.Add(runnableProjectTemplate);

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
        public bool TryGetTemplateFromConfigInfo(IFileSystemInfo templateFileConfig, out ITemplate? template, IFileSystemInfo? localeFileConfig = null, IFile? hostTemplateConfigFile = null, string? baselineName = null)
        {
            _ = templateFileConfig ?? throw new ArgumentNullException(nameof(templateFileConfig));
            ILogger logger = templateFileConfig.MountPoint.EnvironmentSettings.Host.LoggerFactory.CreateLogger<RunnableProjectGenerator>();
            try
            {
                IFile templateFile = templateFileConfig as IFile
                    ?? throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_ConfigShouldBeFile, templateFileConfig.GetDisplayPath()));

                SimpleConfigModel templateModel = LoadBaseTemplate(templateFile, logger, baselineName);

                IFile? localeFile = null;
                if (localeFileConfig != null)
                {
                    localeFile = localeFileConfig as IFile
                       ?? throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_LocaleConfigShouldBeFile, localeFileConfig.GetDisplayPath()));
                    try
                    {
                        ILocalizationModel locModel = LocalizationModelDeserializer.Deserialize(localeFile);
                        if (templateModel.VerifyLocalizationModel(locModel, out IEnumerable<string> errorMessages))
                        {
                            templateModel.Localize(locModel);
                        }
                        else
                        {
                            HandleLocalizationValidationError(logger, templateFile, localeFile, errorMessages);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, localeFile.GetDisplayPath());
                        logger.LogDebug("Details: {0}", ex);
                    }
                }
                template = new RunnableProjectTemplate(this, templateModel, localeFile, hostTemplateConfigFile);
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

        /// <summary>
        /// For explicitly data-typed variables, attempt to convert the variable value to the specified type.
        /// Data type names:
        ///     - choice
        ///     - bool
        ///     - float
        ///     - int
        ///     - hex
        ///     - text
        /// The data type names are case insensitive.
        /// </summary>
        /// <returns>Returns the converted value if it can be converted, throw otherwise.</returns>
        internal static object? DataTypeSpecifiedConvertLiteral(IEngineEnvironmentSettings environmentSettings, ITemplateParameter param, string literal, out bool valueResolutionError)
        {
            valueResolutionError = false;

            if (string.Equals(param.DataType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                else
                {
                    bool boolVal = false;
                    // Note: if the literal is ever null, it is probably due to a problem in TemplateCreator.Instantiate()
                    // which takes care of making null bool -> true as appropriate.
                    // This else can also happen if there is a value but it can't be converted.
                    string val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (environmentSettings.Host.OnParameterError(param, string.Empty, "ParameterValueNotSpecified", out val) && !bool.TryParse(val, out boolVal))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !bool.TryParse(val, out boolVal);
                    return boolVal;
                }
            }
            else if (string.Equals(param.DataType, "choice", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveChoiceValue(literal, param, out string? match))
                {
                    return match;
                }

                if (literal == null && param.Priority != TemplateParameterPriority.Required)
                {
                    return param.DefaultValue;
                }

                string? val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                while (environmentSettings.Host.OnParameterError(param, string.Empty, "ValueNotValid:" + string.Join(",", param.Choices!.Keys), out val)
#pragma warning restore CS0618 // Type or member is obsolete
                        && !TryResolveChoiceValue(literal, param, out val))
                {
                }

                valueResolutionError = val == null;
                return val;
            }
            else if (string.Equals(param.DataType, "float", StringComparison.OrdinalIgnoreCase))
            {
                if (ParserExtensions.DoubleTryParseСurrentOrInvariant(literal, out double convertedFloat))
                {
                    return convertedFloat;
                }
                else
                {
                    string val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (environmentSettings.Host.OnParameterError(param, string.Empty, "ValueNotValidMustBeFloat", out val) && (val == null || !ParserExtensions.DoubleTryParseСurrentOrInvariant(val, out convertedFloat)))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !ParserExtensions.DoubleTryParseСurrentOrInvariant(val, out convertedFloat);
                    return convertedFloat;
                }
            }
            else if (string.Equals(param.DataType, "int", StringComparison.OrdinalIgnoreCase)
                || string.Equals(param.DataType, "integer", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(literal, out long convertedInt))
                {
                    return convertedInt;
                }
                else
                {
                    string val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (environmentSettings.Host.OnParameterError(param, string.Empty, "ValueNotValidMustBeInteger", out val) && (val == null || !long.TryParse(val, out convertedInt)))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !long.TryParse(val, out convertedInt);
                    return convertedInt;
                }
            }
            else if (string.Equals(param.DataType, "hex", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(literal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long convertedHex))
                {
                    return convertedHex;
                }
                else
                {
                    string val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (environmentSettings.Host.OnParameterError(param, string.Empty, "ValueNotValidMustBeHex", out val) && (val == null || val.Length < 3 || !long.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out convertedHex)))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !long.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out convertedHex);
                    return convertedHex;
                }
            }
            else if (string.Equals(param.DataType, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(param.DataType, "string", StringComparison.OrdinalIgnoreCase))
            {
                // "text" is a valid data type, but doesn't need any special handling.
                return literal;
            }
            else
            {
                return literal;
            }
        }

        internal static object? InferTypeAndConvertLiteral(string literal)
        {
            if (literal == null)
            {
                return null;
            }

            if (!literal.Contains("\""))
            {
                if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if ((literal.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
                    || literal.Contains(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator))
                    && ParserExtensions.DoubleTryParseСurrentOrInvariant(literal, out double literalDouble))
                {
                    return literalDouble;
                }

                if (long.TryParse(literal, out long literalLong))
                {
                    return literalLong;
                }

                if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(literal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out literalLong))
                {
                    return literalLong;
                }
            }

            return literal;
        }

        internal static object? InternalConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
        {
            if (untypedValue == null)
            {
                valueResolutionError = false;
                return null;
            }

            if (!string.IsNullOrEmpty(parameter.DataType))
            {
                object? convertedValue = DataTypeSpecifiedConvertLiteral(environmentSettings, parameter, untypedValue, out valueResolutionError);
                return convertedValue;
            }
            else
            {
                valueResolutionError = false;
                return InferTypeAndConvertLiteral(untypedValue);
            }
        }

        /// <summary>
        /// The method checks if all the sources defined in <paramref name="templateConfig"/> are valid.
        /// Example: the source directory should exist, should not be a file, should be accessible via mount point.
        /// </summary>
        /// <param name="templateConfig">template configuration.</param>
        /// <returns>list of found errors.</returns>
        internal IEnumerable<string> ValidateTemplateSourcePaths(SimpleConfigModel templateConfig)
        {
            List<string> errors = new List<string>();
            if (templateConfig.TemplateSourceRoot == null)
            {
                errors.Add(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource);
            }
            // check if any sources get out of the mount point
            foreach (FileSourceMatchInfo source in ((IRunnableProjectConfig)templateConfig).Sources)
            {
                try
                {
                    IFile file = templateConfig.TemplateSourceRoot.FileInfo(source.Source);
                    //template source root should not be a file
                    if (file?.Exists ?? false)
                    {
                        errors.Add(string.Format(LocalizableStrings.Authoring_SourceMustBeDirectory, source.Source));
                    }
                    else
                    {
                        IDirectory sourceRoot = templateConfig.TemplateSourceRoot.DirectoryInfo(source.Source);
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

        private static ICreationResult GetCreationResult(IEngineEnvironmentSettings environmentSettings, RunnableProjectTemplate template, IVariableCollection variables)
        {
            return new CreationResult(
                postActions: PostAction.ListFromModel(environmentSettings, template.Config.PostActionModels, variables),
                primaryOutputs: CreationPath.ListFromModel(environmentSettings, template.Config.PrimaryOutputs, variables));
        }

        // Note the deferred-config macros (generated) are part of the runConfig.Macros
        //      and not in the ComputedMacros.
        //  Possibly make a separate property for the deferred-config macros
        private static void ProcessMacros(IEngineEnvironmentSettings environmentSettings, IGlobalRunConfig runConfig, IParameterSet parameters)
        {
            if (runConfig.Macros != null)
            {
                IVariableCollection varsForMacros = VariableCollection.SetupVariables(environmentSettings, parameters, runConfig.VariableSetup);
                MacrosOperationConfig macroProcessor = new MacrosOperationConfig();
                macroProcessor.ProcessMacros(environmentSettings, runConfig.Macros, varsForMacros, parameters);
            }

            if (runConfig.ComputedMacros != null)
            {
                IVariableCollection varsForMacros = VariableCollection.SetupVariables(environmentSettings, parameters, runConfig.VariableSetup);
                MacrosOperationConfig macroProcessor = new MacrosOperationConfig();
                macroProcessor.ProcessMacros(environmentSettings, runConfig.ComputedMacros, varsForMacros, parameters);
            }
        }

        private static bool TryResolveChoiceValue(string? literal, ITemplateParameter param, out string? match)
        {
            if (literal == null || param.Choices == null)
            {
                match = null;
                return false;
            }

            string? partialMatch = null;

            foreach (string choiceValue in param.Choices.Keys)
            {
                if (string.Equals(choiceValue, literal, StringComparison.OrdinalIgnoreCase))
                {
                    // exact match is good, regardless of partial matches
                    match = choiceValue;
                    return true;
                }
                else if (choiceValue.StartsWith(literal, StringComparison.OrdinalIgnoreCase))
                {
                    if (partialMatch == null)
                    {
                        partialMatch = choiceValue;
                    }
                    else
                    {
                        // multiple partial matches, can't take one.
                        match = null;
                        return false;
                    }
                }
            }

            match = partialMatch;
            return match != null;
        }

        /// <summary>
        /// Loads base template configuration from <paramref name="templateFile"/> and performs its validation.
        /// </summary>
        /// <exception cref="NotSupportedException">when the version of the template is not supported by the generator.</exception>
        /// <exception cref="TemplateValidationException">on validation error.</exception>
        private SimpleConfigModel LoadBaseTemplate(IFile templateFile, ILogger logger, string? baselineName = null)
        {
            ISimpleConfigModifiers? configModifiers = null;
            if (!string.IsNullOrWhiteSpace(baselineName))
            {
                configModifiers = new SimpleConfigModifiers(baselineName!);
            }
            SimpleConfigModel templateModel = new SimpleConfigModel(templateFile, configModifiers);

            CheckGeneratorVersionRequiredByTemplate(templateModel.GeneratorVersions);
            PerformTemplateValidation(templateModel, templateFile, logger);
            return templateModel;
        }

        private void CheckGeneratorVersionRequiredByTemplate(string generatorVersionsAllowed)
        {
            if (string.IsNullOrWhiteSpace(generatorVersionsAllowed))
            {
                return;
            }

            if (!VersionStringHelpers.TryParseVersionSpecification(generatorVersionsAllowed, out IVersionSpecification versionChecker) || !versionChecker.CheckIfVersionIsValid(GeneratorVersion))
            {
                throw new NotSupportedException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateVersionNotSupported, generatorVersionsAllowed, GeneratorVersion));
            }
        }

        private IFile? FindBestHostTemplateConfigFile(IEngineEnvironmentSettings engineEnvironment, IFile config)
        {
            IDictionary<string, IFile> allHostFilesForTemplate = new Dictionary<string, IFile>();

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

        /// <summary>
        /// The method validates loaded <paramref name="templateModel"/>. The errors and warnings are printed using <paramref name="logger"/>.
        /// The warning messages is mainly only for template authoring, and should be used for template validation, so for now they are logged in debug level only.
        /// https://github.com/dotnet/templating/issues/2623.
        /// </summary>
        /// <param name="templateModel">the template configuration to validate.</param>
        /// <param name="templateFile">the template configuration file.</param>
        /// <param name="logger">the logger to use to log the errors.</param>
        /// <exception cref="TemplateValidationException">in case of validation fails.</exception>
        private void PerformTemplateValidation(SimpleConfigModel templateModel, IFile templateFile, ILogger logger)
        {
            //Do some basic checks...
            List<string> errorMessages = new List<string>();
            List<string> warningMessages = new List<string>();

            #region Errors
            if (string.IsNullOrWhiteSpace(templateModel.Identity))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "identity"));
            }

            if (string.IsNullOrWhiteSpace(templateModel.Name))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "name"));
            }

            if ((templateModel.ShortNameList?.Count ?? 0) == 0)
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "shortName"));
            }
            errorMessages.AddRange(ValidateTemplateSourcePaths(templateModel));
            #endregion

            #region Warnings
            //TODO: the warning messages should be transferred to validate subcommand, as they are not useful for final user, but useful for template author.
            //https://github.com/dotnet/templating/issues/2623
            if (string.IsNullOrWhiteSpace(templateModel.SourceName))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "sourceName"));
            }

            if (string.IsNullOrWhiteSpace(templateModel.Author))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "author"));
            }

            if (string.IsNullOrWhiteSpace(templateModel.GroupIdentity))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "groupIdentity"));
            }

            if (string.IsNullOrWhiteSpace(templateModel.GeneratorVersions))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "generatorVersions"));
            }

            if (templateModel.Precedence == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "precedence"));
            }

            if ((templateModel.Classifications?.Count ?? 0) == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "classifications"));
            }

            if (templateModel.PostActionModels != null && templateModel.PostActionModels.Any(x => x.ManualInstructionInfo == null || x.ManualInstructionInfo.Count == 0))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MalformedPostActionManualInstructions));
            }
            #endregion

            if (warningMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateMissingCommonInformation, templateFile.GetDisplayPath()));
                foreach (string message in warningMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                logger.LogDebug(stringBuilder.ToString());
            }

            if (errorMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateNotInstalled, templateFile.GetDisplayPath()));
                foreach (string message in errorMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                logger.LogError(stringBuilder.ToString());
                throw new TemplateValidationException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateValidationFailed, templateFile.GetDisplayPath()));
            }
        }

        private void HandleLocalizationValidationError(
            ILogger logger,
            IFile baseConfiguration,
            IFile localizationConfiguration,
            IEnumerable<string> errorMessages)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format(LocalizableStrings.RunnableProjectGenerator_Warning_LocFileSkipped, localizationConfiguration.GetDisplayPath(), baseConfiguration.GetDisplayPath()));
            foreach (string errorMessage in errorMessages)
            {
                stringBuilder.AppendLine("  " + errorMessage);
            }
            logger.LogWarning(stringBuilder.ToString());
        }

        internal class ParameterSet : IParameterSet
        {
            private readonly IDictionary<string, ITemplateParameter> _parameters = new Dictionary<string, ITemplateParameter>(StringComparer.OrdinalIgnoreCase);

            internal ParameterSet(IRunnableProjectConfig config)
            {
                foreach (KeyValuePair<string, Parameter> p in config.Parameters)
                {
                    p.Value.Name = p.Key;
                    _parameters[p.Key] = p.Value;
                }
            }

            public IEnumerable<ITemplateParameter> ParameterDefinitions => _parameters.Values;

            public IDictionary<ITemplateParameter, object> ResolvedValues { get; } = new Dictionary<ITemplateParameter, object>();

            public bool TryGetParameterDefinition(string name, out ITemplateParameter parameter)
            {
                if (_parameters.TryGetValue(name, out parameter))
                {
                    return true;
                }

                parameter = new Parameter
                {
                    Name = name,
                    Priority = TemplateParameterPriority.Optional,
                    IsVariable = true,
                    Type = "string"
                };

                return true;
            }

            internal void AddParameter(ITemplateParameter param)
            {
                _parameters[param.Name] = param;
            }
        }

        private class TemplateValidationException : Exception
        {
            internal TemplateValidationException(string message) : base(message) { }
        }

    }
}
