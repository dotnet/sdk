// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime;
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
    public sealed class RunnableProjectGenerator : IGenerator
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        internal const string TemplateConfigDirectoryName = ".template.config";
        internal const string TemplateConfigFileName = "template.json";
        internal const string LocalizationFilePrefix = "templatestrings.";
        internal const string LocalizationFileExtension = ".json";
        internal const string GeneratorVersion = "1.0.0.0";
        private static readonly Guid GeneratorId = new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        Guid IIdentifiedComponent.Id => GeneratorId;

        /// <summary>
        /// Converts the raw, string version of a parameter to a strongly typed value. If the parameter has a datatype specified, use that. Otherwise attempt to infer the type.
        /// Throws a TemplateParamException if the conversion fails for any reason.
        /// </summary>
        object? IGenerator.ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
        {
            return InternalConvertParameterValueToType(environmentSettings, parameter, untypedValue, out valueResolutionError);
        }

        Task<ICreationResult> IGenerator.CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate templateData,
            IParameterSet parameters,
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
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunnableProjectConfig templateConfig = (RunnableProjectConfig)templateData;
            if (templateData.TemplateSourceRoot is null)
            {
                throw new InvalidOperationException($"{nameof(templateData.TemplateSourceRoot)} cannot be null to continue.");
            }

            IVariableCollection variables = SetupVariables(parameters, templateConfig.OperationConfig.VariableSetup);
            await templateConfig.EvaluateBindSymbolsAsync(environmentSettings, variables, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ProcessMacros(environmentSettings, templateConfig.OperationConfig, variables);
            templateConfig.Evaluate(variables);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(environmentSettings.Host.Logger, environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateData.TemplateSourceRoot, environmentSettings.Components, variables, templateConfig.OperationConfig, templateConfig.SpecialOperationConfig, templateConfig.IgnoreFileNames);
            List<IFileChange2> changes = new List<IFileChange2>();

            foreach (FileSourceMatchInfo source in templateConfig.Sources)
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

            return new CreationEffects2(changes, GetCreationResult(environmentSettings.Host.Logger, templateConfig, variables));
        }

        IParameterSet IGenerator.GetParametersForTemplate(IEngineEnvironmentSettings environmentSettings, ITemplate template)
        {
            RunnableProjectConfig templateConfig = (RunnableProjectConfig)template;
            return new ParameterSet(templateConfig);
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
                                ILocalizationModel locModel = LocalizationModelDeserializer.Deserialize(locFile);
                                if (templateConfiguration.VerifyLocalizationModel(locModel, locFile))
                                {
                                    localizations.Add(new LocalizationLocator(
                                        locale,
                                        locFile.FullPath,
                                        templateConfiguration.Identity,
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
                    string? val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (environmentSettings.Host.OnParameterError(param, string.Empty, "ParameterValueNotSpecified", out val) && !bool.TryParse(val, out boolVal))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !bool.TryParse(val, out boolVal);
                    return boolVal;
                }
            }
            else if (param.IsChoice())
            {
                if (param.AllowMultipleValues)
                {
                    List<string> val =
                        literal
                            .TokenizeMultiValueParameter()
                            .Select(t => ResolveChoice(environmentSettings, t, param))
                            .Where(r => !string.IsNullOrEmpty(r))
                            .Select(r => r!)
                            .ToList();
                    if (val.Count <= 1)
                    {
                        return val.Count == 0 ? string.Empty : val[0];
                    }

                    return new MultiValueParameter(val);
                }
                else
                {
                    string? val = ResolveChoice(environmentSettings, literal, param);
                    valueResolutionError = val == null;
                    return val;
                }
            }
            else if (string.Equals(param.DataType, "float", StringComparison.OrdinalIgnoreCase))
            {
                if (ParserExtensions.DoubleTryParseСurrentOrInvariant(literal, out double convertedFloat))
                {
                    return convertedFloat;
                }
                else
                {
                    string? val;
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
                    string? val;
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
                    string? val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (environmentSettings.Host.OnParameterError(param, string.Empty, "ValueNotValidMustBeHex", out val) && (val == null || val.Length < 3 || !long.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out convertedHex)))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !long.TryParse(val?.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out convertedHex);
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

        internal async Task<ICreationResult> CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            IRunnableProjectConfig runnableProjectConfig,
            IDirectory templateSourceRoot,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IVariableCollection variables = SetupVariables(parameters, runnableProjectConfig.OperationConfig.VariableSetup);
            await runnableProjectConfig.EvaluateBindSymbolsAsync(environmentSettings, variables, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            ProcessMacros(environmentSettings, runnableProjectConfig.OperationConfig, variables);
            runnableProjectConfig.Evaluate(variables);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(environmentSettings.Host.Logger, environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(templateSourceRoot, environmentSettings.Components, variables, runnableProjectConfig.OperationConfig, runnableProjectConfig.SpecialOperationConfig, runnableProjectConfig.IgnoreFileNames);

            foreach (FileSourceMatchInfo source in runnableProjectConfig.Sources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(targetDirectory, source.Target);
                orchestrator.Run(runSpec, templateSourceRoot.DirectoryInfo(source.Source), target);
            }

            return GetCreationResult(environmentSettings.Host.Logger, runnableProjectConfig, variables);
        }

        private static IVariableCollection SetupVariables(IParameterSet parameters, IVariableConfig variableConfig)
        {
            IVariableCollection variables = VariableCollection.SetupVariables(parameters, variableConfig);

            foreach (Parameter param in parameters.ParameterDefinitions.OfType<Parameter>())
            {
                // Add choice values to variables - to allow them to be recognizable unquoted
                if (param.EnableQuotelessLiterals && param.IsChoice())
                {
                    foreach (string choiceKey in param.Choices.Keys)
                    {
                        if (
                            variables.TryGetValue(choiceKey, out object existingValueObj) &&
                            existingValueObj is string existingValue &&
                            !string.Equals(choiceKey, existingValue, StringComparison.CurrentCulture)
                        )
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
                macroProcessor = macroProcessor ?? new MacrosOperationConfig();
                macroProcessor.ProcessMacros(environmentSettings, runConfig.ComputedMacros, variableCollection);
            }
        }

        private static ICreationResult GetCreationResult(ILogger logger, IRunnableProjectConfig runnableProjectConfig, IVariableCollection variables)
        {
            return new CreationResult(
                postActions: PostAction.ListFromModel(logger, runnableProjectConfig.PostActionModels, variables),
                primaryOutputs: CreationPath.ListFromModel(logger, runnableProjectConfig.PrimaryOutputs, variables));
        }

        private static string? ResolveChoice(IEngineEnvironmentSettings environmentSettings, string? literal, ITemplateParameter param)
        {
            if (TryResolveChoiceValue(literal, param, out string? match))
            {
                return match;
            }

            if (literal == null && param.Priority != TemplateParameterPriority.Required)
            {
                return param.DefaultValue;
            }

#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
            if (
                environmentSettings.Host.OnParameterError(param, string.Empty, "ValueNotValid:" + string.Join(",", param.Choices!.Keys), out string? val)
                && TryResolveChoiceValue(val, param, out string? match2))
            {
                return match2;
            }
#pragma warning restore CS0618 // Type or member is obsolete

            return literal == string.Empty ? string.Empty : null;
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

            public IDictionary<ITemplateParameter, object?> ResolvedValues { get; } = new Dictionary<ITemplateParameter, object?>();

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
    }
}
