// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// The class maps template.json configuration read in <see cref="TemplateConfigModel"/> to runnable configuration.
    /// </summary>
    internal partial class RunnableProjectConfig : IRunnableProjectConfig
    {
        private const string AdditionalConfigFilesIndicator = "AdditionalConfigFiles";
        private static readonly string[] IncludePatternDefaults = new[] { "**/*" };

        private static readonly string[] ExcludePatternDefaults = new[]
        {
            "**/[Bb]in/**",
            "**/[Oo]bj/**",
            "**/" + RunnableProjectGenerator.TemplateConfigDirectoryName + "/**",
            "**/*.filelist",
            "**/*.user",
            "**/*.lock.json"
        };

        private static readonly string[] CopyOnlyPatternDefaults = new[] { "**/node_modules/**" };
        private static readonly string[] DefaultPlaceholderFilenames = new[] { "-.-", "_._" };
        private readonly IEngineEnvironmentSettings _settings;
        private readonly ILogger<RunnableProjectConfig> _logger;
        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new Dictionary<Guid, string>();

        private readonly IGenerator _generator;
        private readonly IFile? _sourceFile;
        private readonly IFile? _localeConfigFile;
        private readonly IFile? _hostConfigFile;

        private IReadOnlyList<FileSourceMatchInfo>? _sources;
        private IGlobalRunConfig? _operationConfig;
        private IReadOnlyList<(string Glob, IGlobalRunConfig RunConfig)>? _specialOperationConfig;
        private IReadOnlyList<IReplacementTokens>? _symbolFilenameReplacements;
        private IReadOnlyList<ICreationPath>? _evaluatedPrimaryOutputs;
        private IReadOnlyList<IPostAction>? _evaluatedPostActions;

        internal RunnableProjectConfig(IEngineEnvironmentSettings settings, IGenerator generator, IFile templateFile, IFile? hostConfigFile = null, IFile? localeConfigFile = null, string? baselineName = null)
        {
            _settings = settings;
            _logger = _settings.Host.LoggerFactory.CreateLogger<RunnableProjectConfig>();
            _generator = generator;
            _sourceFile = templateFile;
            _hostConfigFile = hostConfigFile;
            _localeConfigFile = localeConfigFile;
            if (_sourceFile.Parent?.Parent == null)
            {
                throw new TemplateAuthoringException(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource, string.Empty);
            }
            TemplateSourceRoot = _sourceFile.Parent!.Parent!;

            ConfigurationModel = TemplateConfigModel.FromJObject(
                MergeAdditionalConfiguration(templateFile.ReadJObjectFromIFile(), templateFile),
                _settings.Host.LoggerFactory.CreateLogger<TemplateConfigModel>(),
                baselineName,
                templateFile.GetDisplayPath());

            CheckGeneratorVersionRequiredByTemplate();
            PerformTemplateValidation();

            if (_localeConfigFile != null)
            {
                try
                {
                    LocalizationModel locModel = LocalizationModelDeserializer.Deserialize(_localeConfigFile);
                    if (VerifyLocalizationModel(locModel))
                    {
                        ConfigurationModel.Localize(locModel);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, _localeConfigFile.GetDisplayPath());
                    _logger.LogDebug("Details: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Test constructor.
        /// </summary>
        internal RunnableProjectConfig(IEngineEnvironmentSettings settings, IGenerator generator, TemplateConfigModel configuration, IDirectory templateSource)
        {
            _settings = settings;
            _logger = _settings.Host.LoggerFactory.CreateLogger<RunnableProjectConfig>();
            _generator = generator;
            ConfigurationModel = configuration;
            TemplateSourceRoot = templateSource;
        }

        public IReadOnlyList<IPostAction> PostActions => _evaluatedPostActions ?? throw new InvalidOperationException($"{nameof(Evaluate)} should be called before accessing the property.");

        public IReadOnlyList<ICreationPath> PrimaryOutputs => _evaluatedPrimaryOutputs ?? throw new InvalidOperationException($"{nameof(Evaluate)} should be called before accessing the property.");

        public IFile? SourceFile => _sourceFile;

        public IDirectory TemplateSourceRoot { get; }

        public IReadOnlyList<string> IgnoreFileNames => !string.IsNullOrWhiteSpace(ConfigurationModel.PlaceholderFilename) ? new[] { ConfigurationModel.PlaceholderFilename! } : DefaultPlaceholderFilenames;

        public IReadOnlyList<FileSourceMatchInfo> EvaluatedSources => _sources ?? throw new InvalidOperationException($"{nameof(Evaluate)} should be called before accessing the property.");

        public IGlobalRunConfig GlobalOperationConfig
        {
            get
            {
                if (_operationConfig == null)
                {
                    OperationConfigDefault defaultOperationParams = OperationConfigDefault.DefaultGlobalConfig;
                    _operationConfig = ProduceOperationSetup(defaultOperationParams, true, ConfigurationModel.GlobalCustomOperations);
                }

                return _operationConfig;
            }
        }

        public IReadOnlyList<(string Glob, IGlobalRunConfig RunConfig)> SpecialOperationConfig
        {
            get
            {
                if (_specialOperationConfig == null)
                {
                    IReadOnlyList<OperationConfigDefault> defaultSpecials = OperationConfigDefault.DefaultSpecialConfig;
                    List<(string Glob, IGlobalRunConfig RunConfig)> specialOperationConfig = new();

                    // put the custom configs first in the list
                    HashSet<string> processedGlobs = new HashSet<string>();

                    foreach (CustomFileGlobModel customGlobModel in ConfigurationModel.SpecialCustomOperations)
                    {
                        if (customGlobModel.ConditionResult)
                        {
                            // only add the special if the condition is true
                            OperationConfigDefault defaultParams = defaultSpecials.FirstOrDefault(x => x.Glob == customGlobModel.Glob);

                            defaultParams ??= OperationConfigDefault.Default;

                            IGlobalRunConfig runConfig = ProduceOperationSetup(defaultParams, false, customGlobModel);
                            specialOperationConfig.Add((customGlobModel.Glob, runConfig));
                        }

                        // mark this special as already processed, so it doesn't get included with the defaults
                        // even if the special was skipped due to its custom condition.
                        processedGlobs.Add(customGlobModel.Glob);
                    }

                    // add the remaining default configs in the order specified above
                    foreach (OperationConfigDefault defaultParams in defaultSpecials)
                    {
                        if (processedGlobs.Contains(defaultParams.Glob))
                        {
                            // this one was already setup due to a custom config
                            continue;
                        }

                        IGlobalRunConfig runConfig = ProduceOperationSetup(defaultParams, false, null);
                        specialOperationConfig.Add((defaultParams.Glob, runConfig));
                    }

                    _specialOperationConfig = specialOperationConfig;
                }

                return _specialOperationConfig;
            }
        }

        internal IReadOnlyList<IReplacementTokens> SymbolFilenameReplacements
        {
            get
            {
                _symbolFilenameReplacements ??= ProduceSymbolFilenameReplacements();
                return _symbolFilenameReplacements;
            }
        }

        internal TemplateConfigModel ConfigurationModel { get; private set; }

        public void Evaluate(IVariableCollection rootVariableCollection)
        {
            bool stable = false;
            Dictionary<string, bool> computed = new Dictionary<string, bool>();

            while (!stable)
            {
                stable = true;
                foreach (ComputedSymbol symbol in ConfigurationModel.Symbols.OfType<ComputedSymbol>())
                {
                    bool value = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_settings.Host.Logger, symbol.Value, rootVariableCollection);
                    stable &= computed.TryGetValue(symbol.Name, out bool currentValue) && currentValue == value;
                    rootVariableCollection[symbol.Name] = value;
                    computed[symbol.Name] = value;
                }
            }

            // evaluate the file glob (specials) conditions
            // the result is needed for SpecialOperationConfig
            foreach (CustomFileGlobModel fileGlobModel in ConfigurationModel.SpecialCustomOperations)
            {
                fileGlobModel.EvaluateCondition(_settings.Host.Logger, rootVariableCollection);
            }

            rootVariableCollection.TryGetValue(ConfigurationModel.NameSymbol.Name, out object? resolvedNameParamValue);

            _sources = EvaluateSources(rootVariableCollection, resolvedNameParamValue);

            _evaluatedPrimaryOutputs = PrimaryOutput.Evaluate(
                _settings,
                ConfigurationModel.PrimaryOutputs,
                rootVariableCollection,
                ConfigurationModel.SourceName,
                resolvedNameParamValue,
                SymbolFilenameReplacements);

            _evaluatedPostActions = PostAction.Evaluate(_settings.Host.Logger, ConfigurationModel.PostActionModels, rootVariableCollection);
        }

        public Task EvaluateBindSymbolsAsync(IEngineEnvironmentSettings settings, IVariableCollection variableCollection, CancellationToken cancellationToken)
        {
            var bindSymbols = ConfigurationModel.Symbols.OfType<BindSymbol>();
            if (!bindSymbols.Any())
            {
                return Task.FromResult(0);
            }
            BindSymbolEvaluator bindSymbolEvaluator = new BindSymbolEvaluator(settings);

            return bindSymbolEvaluator.EvaluateBindedSymbolsAsync(bindSymbols, variableCollection, cancellationToken);
        }

        /// <summary>
        /// Verifies that the given localization model was correctly constructed
        /// to localize this SimpleConfigModel.
        /// </summary>
        /// <param name="locModel">The localization model to be verified.</param>
        /// <param name="localeFile">localization file (optional), needed to get file name for logging.</param>
        /// <returns>True if the verification succeeds. False otherwise.
        /// Check logs for details in case of a failed verification.</returns>
        internal bool VerifyLocalizationModel(LocalizationModel locModel, IFile? localeFile = null)
        {
            bool validModel = true;
            localeFile ??= _localeConfigFile;
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
                _logger.LogDebug($"Localization file {localeFile?.GetDisplayPath()} is not compatible with base configuration {_sourceFile?.GetDisplayPath()}");
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.RunnableProjectGenerator_Warning_LocFileSkipped, localeFile?.GetDisplayPath(), _sourceFile?.GetDisplayPath()));
                foreach (string errorMessage in errorMessages)
                {
                    stringBuilder.AppendLine("  " + errorMessage);
                }
                _logger.LogWarning(stringBuilder.ToString());
            }
            return validModel;
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

                IFile? partialConfigFile = primarySourceConfig.Parent?.EnumerateFiles(partialConfigFileName, SearchOption.TopDirectoryOnly).FirstOrDefault(x => string.Equals(x.Name, partialConfigFileName));

                if (partialConfigFile == null)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.SimpleConfigModel_AuthoringException_MergeConfiguration_FileNotFound, partialConfigFileName), partialConfigFileName);
                }

                JObject partialConfigJson = partialConfigFile.ReadJObjectFromIFile();
                combinedSource.Merge(partialConfigJson);
            }

            return combinedSource;
        }

        // If the token is a string:
        //      check if its a valid file in the same directory as the sourceFile.
        //          If so, read that files content as the exclude list.
        //          Otherwise returns an array containing the string value as its only entry.
        // Otherwise, interpret the token as an array and return the content.
        private static IReadOnlyList<string> TryReadConfigFromFile(IReadOnlyList<string> configs, IFile? sourceFile, string[] defaultSet)
        {
            if (configs == null || !configs.Any())
            {
                return defaultSet;
            }
            if (configs.Count == 1)
            {
                string singleConfig = configs[0];
                if ((singleConfig.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                    || (!sourceFile?.Parent?.FileInfo(singleConfig)?.Exists ?? true))
                {
                    return configs;
                }
                else
                {
                    using (Stream excludeList = sourceFile!.Parent!.FileInfo(singleConfig)!.OpenRead())
                    using (TextReader reader = new StreamReader(excludeList, Encoding.UTF8, true, 4096, true))
                    {
                        return reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                }
            }
            return configs;
        }

        private IReadOnlyList<IReplacementTokens> ProduceSymbolFilenameReplacements()
        {
            List<IReplacementTokens> filenameReplacements = new();
            if (!ConfigurationModel.Symbols.Any())
            {
                return filenameReplacements;
            }

            foreach (BaseReplaceSymbol symbol in ConfigurationModel.Symbols.OfType<BaseReplaceSymbol>())
            {
                if (symbol is BaseValueSymbol p)
                {
                    if (!string.IsNullOrWhiteSpace(symbol.FileRename))
                    {
                        foreach (string formName in p.Forms.GlobalForms)
                        {
                            if (ConfigurationModel.Forms.TryGetValue(formName, out IValueForm valueForm))
                            {
                                string symbolName = symbol.Name + "{-VALUE-FORMS-}" + formName;
                                string processedFileReplacement = valueForm.Process(symbol.FileRename!, ConfigurationModel.Forms);
                                if (!string.IsNullOrEmpty(processedFileReplacement))
                                {
                                    GenerateFileReplacementsForSymbol(processedFileReplacement!, symbolName, filenameReplacements);
                                }
                            }
                            else
                            {
                                _settings.Host.Logger.LogDebug($"Unable to find a form called '{formName}'");
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(symbol.FileRename))
                    {
                        GenerateFileReplacementsForSymbol(symbol.FileRename!, symbol.Name, filenameReplacements);
                    }
                }
            }
            return filenameReplacements;
        }

        private IGlobalRunConfig ProduceOperationSetup(OperationConfigDefault defaultModel, bool generateMacros, CustomFileGlobModel? customGlobModel = null)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();

            // TODO: if we allow custom config to specify a built-in conditional type, decide what to do.
            if (defaultModel.ConditionalStyle != ConditionalType.None)
            {
                operations.AddRange(ConditionalConfig.ConditionalSetup(defaultModel.ConditionalStyle, defaultModel.Evaluator, true, true, null));
            }

            if (customGlobModel == null || string.IsNullOrEmpty(customGlobModel.FlagPrefix))
            {
                // these conditions may need to be separated - if there is custom info, but the flag prefix was not provided, we might want to raise a warning / error
                operations.AddRange(FlagsConfig.FlagsDefaultSetup(defaultModel.FlagPrefix));
            }
            else
            {
                operations.AddRange(FlagsConfig.FlagsDefaultSetup(customGlobModel.FlagPrefix!));
            }

            IVariableConfig variableConfig = customGlobModel != null ? customGlobModel.VariableFormat : VariableConfig.DefaultVariableSetup();
            List<IMacroConfig>? macros = null;
            List<IMacroConfig>? computedMacros = new List<IMacroConfig>();
            List<IReplacementTokens> macroGeneratedReplacements = new List<IReplacementTokens>();

            if (generateMacros)
            {
                macros = ProduceMacroConfig();
                computedMacros = ProduceComputedMacroConfig();
            }

            foreach (BaseSymbol symbol in ConfigurationModel.Symbols)
            {
                if (symbol is DerivedSymbol derivedSymbol)
                {
                    if (generateMacros)
                    {
                        macros?.Add(new ProcessValueFormMacroConfig(derivedSymbol.ValueSource, symbol.Name, derivedSymbol.DataType, derivedSymbol.ValueTransform, ConfigurationModel.Forms));
                    }
                }

                string sourceVariable = symbol.Name;

                if (symbol is BaseReplaceSymbol replaceSymbol)
                {
                    if (symbol is BaseValueSymbol baseValueSymbol)
                    {
                        foreach (string formName in baseValueSymbol.Forms.GlobalForms)
                        {
                            if (ConfigurationModel.Forms.TryGetValue(formName, out IValueForm valueForm))
                            {
                                string symbolName = sourceVariable + "{-VALUE-FORMS-}" + formName;
                                if (!string.IsNullOrWhiteSpace(replaceSymbol.Replaces))
                                {
                                    string processedReplacement = valueForm.Process(baseValueSymbol.Replaces!, ConfigurationModel.Forms);
                                    if (!string.IsNullOrEmpty(processedReplacement))
                                    {
                                        GenerateReplacementsForSymbol(replaceSymbol, processedReplacement!, symbolName, macroGeneratedReplacements);
                                    }
                                }
                                if (generateMacros)
                                {
                                    macros?.Add(new ProcessValueFormMacroConfig(sourceVariable, symbolName, "string", formName, ConfigurationModel.Forms));
                                }
                            }
                            else
                            {
                                _settings.Host.Logger.LogDebug($"Unable to find a form called '{formName}'");
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(replaceSymbol.Replaces))
                    {
                        GenerateReplacementsForSymbol(replaceSymbol, replaceSymbol.Replaces!, sourceVariable, macroGeneratedReplacements);
                    }
                }
            }
            foreach (KeyValuePair<Guid, string> map in _guidToGuidPrefixMap)
            {
                foreach (char format in GuidMacroConfig.DefaultFormats)
                {
                    bool isUpperCase = char.IsUpper(format);
                    string newGuid = map.Key.ToString(format.ToString());
                    newGuid = isUpperCase ? newGuid.ToUpperInvariant() : newGuid.ToLowerInvariant();
                    string replacementKey = map.Value + (isUpperCase ? GuidMacroConfig.UpperCaseDenominator : GuidMacroConfig.LowerCaseDenominator) + format;
                    macroGeneratedReplacements.Add(new ReplacementTokens(replacementKey, newGuid.TokenConfig()));
                }
            }

            IReadOnlyList<CustomOperationModel> customOperationConfig = customGlobModel != null && customGlobModel.Operations != null ? customGlobModel.Operations : new List<CustomOperationModel>();
            foreach (IOperationProvider p in operations.ToList())
            {
                if (!string.IsNullOrEmpty(p.Id))
                {
                    string? prefix = (customGlobModel == null || string.IsNullOrEmpty(customGlobModel.FlagPrefix)) ? defaultModel.FlagPrefix : customGlobModel.FlagPrefix;
                    string on = $"{prefix}+:{p.Id}";
                    string off = $"{prefix}-:{p.Id}";
                    string onNoEmit = $"{prefix}+:{p.Id}:noEmit";
                    string offNoEmit = $"{prefix}-:{p.Id}:noEmit";
                    operations.Add(new SetFlag(p.Id, on.TokenConfig(), off.TokenConfig(), onNoEmit.TokenConfig(), offNoEmit.TokenConfig(), null, true));
                }
            }

            GlobalRunConfig config = new GlobalRunConfig()
            {
                Operations = operations,
                VariableSetup = variableConfig,
                Macros = (macros as IReadOnlyList<IMacroConfig>) ?? Array.Empty<IMacroConfig>(),
                ComputedMacros = computedMacros,
                Replacements = macroGeneratedReplacements,
                CustomOperations = customOperationConfig
            };
            return config;
        }

        private void GenerateReplacementsForSymbol(BaseReplaceSymbol symbol, string replaces, string sourceVariable, List<IReplacementTokens> macroGeneratedReplacements)
        {
            TokenConfig replacementConfig = replaces.TokenConfigBuilder();
            if (symbol.ReplacementContexts.Count > 0)
            {
                foreach (ReplacementContext context in symbol.ReplacementContexts)
                {
                    TokenConfig builder = replacementConfig;
                    if (!string.IsNullOrEmpty(context.OnlyIfAfter))
                    {
                        builder = builder.OnlyIfAfter(context.OnlyIfAfter);
                    }

                    if (!string.IsNullOrEmpty(context.OnlyIfBefore))
                    {
                        builder = builder.OnlyIfBefore(context.OnlyIfBefore);
                    }

                    macroGeneratedReplacements.Add(new ReplacementTokens(sourceVariable, builder));
                }
            }
            else
            {
                macroGeneratedReplacements.Add(new ReplacementTokens(sourceVariable, replacementConfig));
            }
        }

        private void GenerateFileReplacementsForSymbol(string fileRename, string sourceVariable, List<IReplacementTokens> filenameReplacements)
        {
            TokenConfig replacementConfig = fileRename.TokenConfigBuilder();
            filenameReplacements.Add(new ReplacementTokens(sourceVariable, replacementConfig));
        }

        private List<IMacroConfig> ProduceMacroConfig()
        {
            List<IMacroConfig> generatedMacroConfigs = new List<IMacroConfig>();

            if (ConfigurationModel.Guids != null)
            {
                int guidCount = 0;
                foreach (Guid guid in ConfigurationModel.Guids)
                {
                    int id = guidCount++;
                    string replacementId = "guid" + id;
                    generatedMacroConfigs.Add(new GuidMacroConfig(replacementId, "string", null, null));
                    _guidToGuidPrefixMap[guid] = replacementId;
                }
            }

            foreach (GeneratedSymbol symbol in ConfigurationModel.Symbols.OfType<GeneratedSymbol>())
            {
                string type = symbol.Generator;
                string variableName = symbol.Name;
                Dictionary<string, JToken> configParams = new Dictionary<string, JToken>();

                foreach (KeyValuePair<string, string> parameter in symbol.Parameters)
                {
                    configParams.Add(parameter.Key, JToken.Parse(parameter.Value));
                }

                string? dataType = symbol.DataType;

                if (string.Equals(dataType, "choice", StringComparison.OrdinalIgnoreCase))
                {
                    dataType = "string";
                }

                generatedMacroConfigs.Add(new GeneratedSymbolDeferredMacroConfig(type, dataType, variableName, configParams));
            }

            return generatedMacroConfigs;
        }

        private List<IMacroConfig> ProduceComputedMacroConfig()
        {
            List<IMacroConfig> computedMacroConfigs = new List<IMacroConfig>();
            foreach (ComputedSymbol symbol in ConfigurationModel.Symbols.OfType<ComputedSymbol>())
            {
                string value = symbol.Value;
                string? evaluator = symbol.Evaluator;
                computedMacroConfigs.Add(new EvaluateMacroConfig(symbol.Name, "bool", value, evaluator));
            }

            return computedMacroConfigs;
        }

        private List<FileSourceMatchInfo> EvaluateSources(IVariableCollection rootVariableCollection, object? resolvedNameParamValue)
        {
            List<FileSourceMatchInfo> sources = new();

            foreach (ExtendedFileSource source in ConfigurationModel.Sources)
            {
                if (!source.EvaluateCondition(_settings.Host.Logger, rootVariableCollection))
                {
                    continue;
                }

                IReadOnlyList<string> topIncludePattern = TryReadConfigFromFile(source.Include, SourceFile, IncludePatternDefaults).ToList();
                IReadOnlyList<string> topExcludePattern = TryReadConfigFromFile(source.Exclude, SourceFile, ExcludePatternDefaults).ToList();
                IReadOnlyList<string> topCopyOnlyPattern = TryReadConfigFromFile(source.CopyOnly, SourceFile, CopyOnlyPatternDefaults).ToList();
                FileSourceEvaluable topLevelPatterns = new FileSourceEvaluable(topIncludePattern, topExcludePattern, topCopyOnlyPattern);

                Dictionary<string, string> fileRenamesFromSource = source.Rename.ToDictionary(x => x.Key, x => x.Value);
                List<FileSourceEvaluable> modifierList = new List<FileSourceEvaluable>();

                if (source.Modifiers != null)
                {
                    foreach (SourceModifier modifier in source.Modifiers)
                    {
                        if (modifier.EvaluateCondition(_settings.Host.Logger, rootVariableCollection))
                        {
                            IReadOnlyList<string> modifierIncludes = TryReadConfigFromFile(modifier.Include, SourceFile, Array.Empty<string>());
                            IReadOnlyList<string> modifierExcludes = TryReadConfigFromFile(modifier.Exclude, SourceFile, Array.Empty<string>());
                            IReadOnlyList<string> modifierCopyOnly = TryReadConfigFromFile(modifier.CopyOnly, SourceFile, Array.Empty<string>());
                            FileSourceEvaluable modifierPatterns = new FileSourceEvaluable(modifierIncludes, modifierExcludes, modifierCopyOnly);
                            modifierList.Add(modifierPatterns);

                            if (modifier.Rename != null)
                            {
                                foreach (KeyValuePair<string, string> property in modifier.Rename)
                                {
                                    fileRenamesFromSource[property.Key] = property.Value;
                                }
                            }
                        }
                    }
                }

                string sourceDirectory = source.Source;
                string targetDirectory = source.Target;
                IReadOnlyDictionary<string, string> allRenamesForSource = AugmentRenames(sourceDirectory, ref targetDirectory, resolvedNameParamValue, rootVariableCollection, fileRenamesFromSource);

                FileSourceMatchInfo sourceMatcher = new FileSourceMatchInfo(
                    sourceDirectory,
                    targetDirectory,
                    topLevelPatterns,
                    allRenamesForSource,
                    modifierList);
                sources.Add(sourceMatcher);
            }

            if (ConfigurationModel.Sources.Count == 0)
            {
                IReadOnlyList<string> includePattern = IncludePatternDefaults;
                IReadOnlyList<string> excludePattern = ExcludePatternDefaults;
                IReadOnlyList<string> copyOnlyPattern = CopyOnlyPatternDefaults;
                FileSourceEvaluable topLevelPatterns = new FileSourceEvaluable(includePattern, excludePattern, copyOnlyPattern);

                string targetDirectory = string.Empty;
                Dictionary<string, string> fileRenamesFromSource = new Dictionary<string, string>(StringComparer.Ordinal);
                IReadOnlyDictionary<string, string> allRenamesForSource = AugmentRenames("./", ref targetDirectory, resolvedNameParamValue, rootVariableCollection, fileRenamesFromSource);

                FileSourceMatchInfo sourceMatcher = new FileSourceMatchInfo(
                    "./",
                    "./",
                    topLevelPatterns,
                    allRenamesForSource,
                    new List<FileSourceEvaluable>());
                sources.Add(sourceMatcher);
            }

            return sources;
        }

        private IReadOnlyDictionary<string, string> AugmentRenames(
            string sourceDirectory,
            ref string targetDirectory,
            object? resolvedNameParamValue,
            IVariableCollection variables,
            Dictionary<string, string> fileRenames)
        {
            return FileRenameGenerator.AugmentFileRenames(
                _settings,
                ConfigurationModel.SourceName,
                TemplateSourceRoot,
                sourceDirectory,
                ref targetDirectory,
                resolvedNameParamValue,
                variables,
                fileRenames,
                SymbolFilenameReplacements);
        }

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
#pragma warning disable SA1202 // Elements should be ordered by access
        internal void PerformTemplateValidation()
#pragma warning restore SA1202 // Elements should be ordered by access
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
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateMissingCommonInformation, SourceFile?.GetDisplayPath()));
                foreach (string message in warningMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                _logger.LogDebug(stringBuilder.ToString());
            }

            if (errorMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateNotInstalled, SourceFile?.GetDisplayPath()));
                foreach (string message in errorMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                _logger.LogError(stringBuilder.ToString());
                throw new TemplateValidationException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateValidationFailed, SourceFile?.GetDisplayPath()));
            }
        }

        /// <summary>
        /// The method checks if all the sources are valid.
        /// Example: the source directory should exist, should not be a file, should be accessible via mount point.
        /// </summary>
        /// <returns>list of found errors.</returns>
        internal IEnumerable<string> ValidateTemplateSourcePaths()
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
    }
}
