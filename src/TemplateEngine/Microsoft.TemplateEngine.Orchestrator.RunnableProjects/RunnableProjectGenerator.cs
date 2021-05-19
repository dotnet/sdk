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
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectGenerator : IGenerator
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        internal const string TemplateConfigDirectoryName = ".template.config";
        internal const string TemplateConfigFileName = "template.json";
        internal const string LocalizationFilePrefix = "templatestrings.";
        internal const string LocalizationFileExtension = ".json";
        private const string AdditionalConfigFilesIndicator = "AdditionalConfigFiles";
        private const string GeneratorVersion = "1.0.0.0";
        private static readonly Guid GeneratorId = new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        public Guid Id => GeneratorId;

        //
        // Converts the raw, string version of a parameter to a strongly typed value.
        // If the param has a datatype specified, use that. Otherwise attempt to infer the type.
        // Throws a TemplateParamException if the conversion fails for any reason.
        //
        public object ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
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
            template.Config.Evaluate(parameters, variables, template.ConfigFile);

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
            template.Config.Evaluate(parameters, variables, template.ConfigFile);

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

        public IList<ITemplate> GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations)
        {
            IDirectory folder = source.Root;
            IList<ITemplate> templateList = new List<ITemplate>();
            localizations = new List<ILocalizationLocator>();

            foreach (IFile file in folder.EnumerateFiles(TemplateConfigFileName, SearchOption.AllDirectories))
            {
                IFile hostConfigFile = FindBestHostTemplateConfigFile(source.EnvironmentSettings, file);

                if (TryGetTemplateFromConfigInfo(file, out ITemplate template, hostTemplateConfigFile: hostConfigFile))
                {
                    templateList.Add(template);

                    IDirectory localizeFolder = file.Parent.DirectoryInfo("localize");
                    if (localizeFolder != null && localizeFolder.Exists)
                    {
                        foreach (IFile locFile in localizeFolder.EnumerateFiles(LocalizationFilePrefix + "*" + LocalizationFileExtension, SearchOption.AllDirectories))
                        {
                            string locale = locFile.Name.Substring(LocalizationFilePrefix.Length, locFile.Name.Length - LocalizationFilePrefix.Length - LocalizationFileExtension.Length);

                            if (TryGetLangPackFromFile(locFile, out ILocalizationModel locModel))
                            {
                                localizations.Add(new LocalizationLocator(
                                    locale,
                                    locFile.FullPath,
                                    template.Identity,
                                    locModel.Author,
                                    locModel.Name,
                                    locModel.Description,
                                    locModel.ParameterSymbols));
                            }
                        }
                    }
                }
            }

            return templateList;
        }

        public bool TryGetTemplateFromConfigInfo(IFileSystemInfo templateFileConfig, out ITemplate template, IFileSystemInfo localeFileConfig = null, IFile hostTemplateConfigFile = null, string baselineName = null)
        {
            IFile templateFile = templateFileConfig as IFile;

            if (templateFile == null)
            {
                template = null;
                return false;
            }

            IFile localeFile = localeFileConfig as IFile;
            ITemplateEngineHost host = templateFileConfig.MountPoint.EnvironmentSettings.Host;

            try
            {
                JObject baseSrcObject = ReadJObjectFromIFile(templateFile);
                JObject srcObject = MergeAdditionalConfiguration(baseSrcObject, templateFileConfig);

                JObject localeSourceObject = null;
                if (localeFile != null)
                {
                    localeSourceObject = ReadJObjectFromIFile(localeFile);
                }

                ISimpleConfigModifiers configModifiers = new SimpleConfigModifiers(baselineName);
                SimpleConfigModel templateModel = SimpleConfigModel.FromJObject(templateFile.MountPoint.EnvironmentSettings, srcObject, configModifiers, localeSourceObject);

                if (!PerformTemplateValidation(templateModel, templateFile, host))
                {
                    template = null;
                    return false;
                }

                if (!CheckGeneratorVersionRequiredByTemplate(templateModel.GeneratorVersions))
                {
                    // template isn't compatible with this generator version
                    template = null;
                    return false;
                }

                RunnableProjectTemplate runnableProjectTemplate = new RunnableProjectTemplate(srcObject, this, templateFile, templateModel, null, hostTemplateConfigFile);
                if (!AreAllTemplatePathsValid(templateFile.MountPoint.EnvironmentSettings, templateModel, runnableProjectTemplate))
                {
                    template = null;
                    return false;
                }

                template = runnableProjectTemplate;
                return true;
            }
            catch (Exception ex)
            {
                host.Logger.LogError($"Error reading template from file: {templateFile.FullPath} | Error = {ex.Message}");
            }

            template = null;
            return false;
        }

        // For explicitly data-typed variables, attempt to convert the variable value to the specified type.
        // Data type names:
        //     - choice
        //     - bool
        //     - float
        //     - int
        //     - hex
        //     - text
        // The data type names are case insensitive.
        //
        // Returns the converted value if it can be converted, throw otherwise
        internal static object DataTypeSpecifiedConvertLiteral(IEngineEnvironmentSettings environmentSettings, ITemplateParameter param, string literal, out bool valueResolutionError)
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
                    while (environmentSettings.Host.OnParameterError(param, null, "ParameterValueNotSpecified", out val) && !bool.TryParse(val, out boolVal))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                    }

                    valueResolutionError = !bool.TryParse(val, out boolVal);
                    return boolVal;
                }
            }
            else if (string.Equals(param.DataType, "choice", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveChoiceValue(literal, param, out string match))
                {
                    return match;
                }

                if (literal == null && param.Priority != TemplateParameterPriority.Required)
                {
                    return param.DefaultValue;
                }

                string val;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                while (environmentSettings.Host.OnParameterError(param, null, "ValueNotValid:" + string.Join(",", param.Choices.Keys), out val)
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
                    while (environmentSettings.Host.OnParameterError(param, null, "ValueNotValidMustBeFloat", out val) && (val == null || !ParserExtensions.DoubleTryParseСurrentOrInvariant(val, out convertedFloat)))
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
                    while (environmentSettings.Host.OnParameterError(param, null, "ValueNotValidMustBeInteger", out val) && (val == null || !long.TryParse(val, out convertedInt)))
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
                    while (environmentSettings.Host.OnParameterError(param, null, "ValueNotValidMustBeHex", out val) && (val == null || val.Length < 3 || !long.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out convertedHex)))
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

        internal static object InferTypeAndConvertLiteral(string literal)
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

        internal static object InternalConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError)
        {
            if (untypedValue == null)
            {
                valueResolutionError = false;
                return null;
            }

            if (!string.IsNullOrEmpty(parameter.DataType))
            {
                object convertedValue = DataTypeSpecifiedConvertLiteral(environmentSettings, parameter, untypedValue, out valueResolutionError);
                return convertedValue;
            }
            else
            {
                valueResolutionError = false;
                return InferTypeAndConvertLiteral(untypedValue);
            }
        }

        // TODO: localize the diagnostic strings
        // checks that all the template sources are under the template root, and they exist.
        internal bool AreAllTemplatePathsValid(IEngineEnvironmentSettings environmentSettings, IRunnableProjectConfig templateConfig, ITemplate runnableTemplate)
        {
            ILogger logger = environmentSettings.Host.Logger;

            if (runnableTemplate.TemplateSourceRoot == null)
            {
                logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource, runnableTemplate.Name));
                return false;
            }

            // check if any sources get out of the mount point
            bool allSourcesValid = true;
            foreach (FileSourceMatchInfo source in templateConfig.Sources)
            {
                try
                {
                    IFile file = runnableTemplate.TemplateSourceRoot.FileInfo(source.Source);

                    if (file?.Exists ?? false)
                    {
                        allSourcesValid = false;
                        logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateNameDisplay, runnableTemplate.Name));
                        logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateSourceRoot, runnableTemplate.TemplateSourceRoot.FullPath));
                        logger.LogDebug(string.Format(LocalizableStrings.Authoring_SourceMustBeDirectory, source.Source));
                    }
                    else
                    {
                        IDirectory sourceRoot = runnableTemplate.TemplateSourceRoot.DirectoryInfo(source.Source);

                        if (!(sourceRoot?.Exists ?? false))
                        {
                            // non-existant directory
                            allSourcesValid = false;
                            logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateNameDisplay, runnableTemplate.Name));
                            logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateSourceRoot, runnableTemplate.TemplateSourceRoot.FullPath));
                            logger.LogDebug(string.Format(LocalizableStrings.Authoring_SourceDoesNotExist, source.Source));
                            logger.LogDebug(string.Format(LocalizableStrings.Authoring_SourceIsOutsideInstallSource, sourceRoot.FullPath));
                        }
                    }
                }
                catch
                {
                    // outside the mount point root
                    // TODO: after the null ref exception in DirectoryInfo is fixed, change how this check works.
                    allSourcesValid = false;
                    logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateNameDisplay, runnableTemplate.Name));
                    logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateSourceRoot, runnableTemplate.TemplateSourceRoot.FullPath));
                    logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource, source.Source));
                }
            }

            return allSourcesValid;
        }

        internal JObject ReadJObjectFromIFile(IFile file)
        {
            using (Stream s = file.OpenRead())
            using (TextReader tr = new StreamReader(s, System.Text.Encoding.UTF8, true))
            using (JsonReader r = new JsonTextReader(tr))
            {
                return JObject.Load(r);
            }
        }

        private static ICreationResult GetCreationResult(IEngineEnvironmentSettings environmentSettings, RunnableProjectTemplate template, IVariableCollection variables)
        {
            return new CreationResult(
                postActions: PostAction.ListFromModel(environmentSettings, template.Config.PostActionModel, variables),
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

        private static bool TryResolveChoiceValue(string literal, ITemplateParameter param, out string match)
        {
            if (literal == null)
            {
                match = null;
                return false;
            }

            string partialMatch = null;

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

        private bool CheckGeneratorVersionRequiredByTemplate(string generatorVersionsAllowed)
        {
            if (string.IsNullOrEmpty(generatorVersionsAllowed))
            {
                return true;
            }

            if (!VersionStringHelpers.TryParseVersionSpecification(generatorVersionsAllowed, out IVersionSpecification versionChecker))
            {
                return false;
            }

            return versionChecker.CheckIfVersionIsValid(GeneratorVersion);
        }

        private IFile FindBestHostTemplateConfigFile(IEngineEnvironmentSettings engineEnvironment, IFile config)
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

        // Checks the primarySource for additional configuration files.
        // If found, merges them all together.
        // Returns the merged JObject (or the original if there was nothing to merge).
        // Additional files must be in the same dir as the template file.
        private JObject MergeAdditionalConfiguration(JObject primarySource, IFileSystemInfo primarySourceConfig)
        {
            IReadOnlyList<string> otherFiles = primarySource.ArrayAsStrings(AdditionalConfigFilesIndicator);

            if (!otherFiles.Any())
            {
                return primarySource;
            }

            JObject combinedSource = (JObject)primarySource.DeepClone();

            foreach (string partialConfigFileName in otherFiles)
            {
                if (!partialConfigFileName.EndsWith("." + TemplateConfigFileName))
                {
                    throw new TemplateAuthoringException($"Split configuration error with file [{partialConfigFileName}]. Additional configuration file names must end with '.{TemplateConfigFileName}'.", partialConfigFileName);
                }

                IFile partialConfigFile = primarySourceConfig.Parent.EnumerateFiles(partialConfigFileName, SearchOption.TopDirectoryOnly).FirstOrDefault(x => string.Equals(x.Name, partialConfigFileName));

                if (partialConfigFile == null)
                {
                    throw new TemplateAuthoringException($"Split configuration file [{partialConfigFileName}] could not be found.", partialConfigFileName);
                }

                JObject partialConfigJson = ReadJObjectFromIFile(partialConfigFile);
                combinedSource.Merge(partialConfigJson);
            }

            return combinedSource;
        }

        private bool PerformTemplateValidation(SimpleConfigModel templateModel, IFile templateFile, ITemplateEngineHost host)
        {
            //Do some basic checks...
            List<string> errorMessages = new List<string>();
            List<string> warningMessages = new List<string>();
            if (string.IsNullOrEmpty(templateModel.Identity))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "identity", templateFile.FullPath));
            }

            if (string.IsNullOrEmpty(templateModel.Name))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "name", templateFile.FullPath));
            }

            if ((templateModel.ShortNameList?.Count ?? 0) == 0)
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "shortName", templateFile.FullPath));
            }

            if (string.IsNullOrEmpty(templateModel.SourceName))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "sourceName", templateFile.FullPath));
            }

            if (string.IsNullOrEmpty(templateModel.Author))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "author", templateFile.FullPath));
            }

            if (string.IsNullOrEmpty(templateModel.GroupIdentity))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "groupIdentity", templateFile.FullPath));
            }

            if (string.IsNullOrEmpty(templateModel.GeneratorVersions))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "generatorVersions", templateFile.FullPath));
            }

            if (templateModel.Precedence == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "precedence", templateFile.FullPath));
            }

            if ((templateModel.Classifications?.Count ?? 0) == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "classifications", templateFile.FullPath));
            }

            if (templateModel.PostActionModel != null && templateModel.PostActionModel.Any(x => x.ManualInstructionInfo == null || x.ManualInstructionInfo.Count == 0))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MalformedPostActionManualInstructions, templateFile.FullPath));
            }

            if (warningMessages.Count > 0)
            {
                host.Logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateMissingCommonInformation, templateFile.FullPath));

                foreach (string message in warningMessages)
                {
                    host.Logger.LogDebug("    " + message);
                }
            }

            if (errorMessages.Count > 0)
            {
                host.Logger.LogDebug(string.Format(LocalizableStrings.Authoring_TemplateNotInstalled, templateFile.FullPath));

                foreach (string message in errorMessages)
                {
                    host.Logger.LogDebug("    " + message);
                }

                return false;
            }

            return true;
        }

        private bool TryGetLangPackFromFile(IFile file, out ILocalizationModel locModel)
        {
            if (file == null)
            {
                locModel = null;
                return false;
            }

            try
            {
                JObject srcObject = ReadJObjectFromIFile(file);
                locModel = SimpleConfigModel.LocalizationFromJObject(srcObject);
                return true;
            }
            catch (Exception ex)
            {
                ITemplateEngineHost host = file.MountPoint.EnvironmentSettings.Host;
                host.Logger.LogError($"Error reading Langpack from file: {file.FullPath} | Error = {ex.ToString()}");
            }

            locModel = null;
            return false;
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
    }
}
