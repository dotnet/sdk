// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// The class represent the template ready to be run.
    /// The class is disposable, as it holds the mount point to template sources.
    /// </summary>
    internal partial class RunnableProjectConfig : DirectoryBasedTemplate, IRunnableProjectConfig, IDisposable
    {
        protected static readonly string[] IncludePatternDefaults = new[] { "**/*" };

        protected static readonly string[] ExcludePatternDefaults = new[]
        {
            "**/[Bb]in/**",
            "**/[Oo]bj/**",
            "**/" + RunnableProjectGenerator.TemplateConfigDirectoryName + "/**",
            "**/*.filelist",
            "**/*.user",
            "**/*.lock.json"
        };

        protected static readonly string[] CopyOnlyPatternDefaults = new[] { "**/node_modules/**" };
        private static readonly string[] DefaultPlaceholderFilenames = new[] { "-.-", "_._" };
        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new();

        private readonly TemplateLocalizationInfo? _localizationInfo;
        private readonly IFile? _hostConfigFile;

        private IReadOnlyList<FileSourceMatchInfo>? _sources;
        private GlobalRunConfig? _operationConfig;
        private IReadOnlyList<(string Glob, GlobalRunConfig RunConfig)>? _specialOperationConfig;
        private IReadOnlyList<IReplacementTokens>? _symbolFilenameReplacements;
        private IReadOnlyList<ICreationPath>? _evaluatedPrimaryOutputs;
        private IReadOnlyList<IPostAction>? _evaluatedPostActions;

        /// <summary>
        /// Instantiation constructor - loads template with specific localization.
        /// </summary>
        internal RunnableProjectConfig(IEngineEnvironmentSettings settings, IGenerator generator, IFile templateFile, IFile? hostConfigFile = null, IFile? localeConfigFile = null, string? baselineName = null)
            : base(settings, generator, templateFile, baselineName)
        {
            _hostConfigFile = hostConfigFile;

            SourceMountPoint = templateFile.MountPoint;

            if (localeConfigFile != null)
            {
                try
                {
                    LocalizationModel locModel = LocalizationModelDeserializer.Deserialize(localeConfigFile);
                    _localizationInfo = new TemplateLocalizationInfo(ParseLocFileName(localeConfigFile) ?? CultureInfo.InvariantCulture, locModel, localeConfigFile);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(LocalizableStrings.LocalizationModelDeserializer_Error_FailedToParse, localeConfigFile.GetDisplayPath());
                    Logger.LogDebug("Details: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Test constructor.
        /// Loads the template based on <paramref name="configuration"/> instead of reading it from file.
        /// </summary>
        internal RunnableProjectConfig(IEngineEnvironmentSettings settings, IGenerator generator, TemplateConfigModel configuration, IDirectory templateSource) : base(settings, generator, configuration, templateSource)
        {
            SourceMountPoint = templateSource.MountPoint;
        }

        public IReadOnlyList<IPostAction> PostActions => _evaluatedPostActions ?? throw new InvalidOperationException($"{nameof(Evaluate)} should be called before accessing the property.");

        public IReadOnlyList<ICreationPath> PrimaryOutputs => _evaluatedPrimaryOutputs ?? throw new InvalidOperationException($"{nameof(Evaluate)} should be called before accessing the property.");

        public IReadOnlyList<string> IgnoreFileNames => !string.IsNullOrWhiteSpace(ConfigurationModel.PlaceholderFilename) ? new[] { ConfigurationModel.PlaceholderFilename! } : DefaultPlaceholderFilenames;

        public IReadOnlyList<FileSourceMatchInfo> EvaluatedSources => _sources ?? throw new InvalidOperationException($"{nameof(Evaluate)} should be called before accessing the property.");

        public GlobalRunConfig GlobalOperationConfig
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

        public IReadOnlyList<(string Glob, GlobalRunConfig RunConfig)> SpecialOperationConfig
        {
            get
            {
                if (_specialOperationConfig == null)
                {
                    IReadOnlyList<OperationConfigDefault> defaultSpecials = OperationConfigDefault.DefaultSpecialConfig;
                    List<(string Glob, GlobalRunConfig RunConfig)> specialOperationConfig = new();

                    // put the custom configs first in the list
                    HashSet<string> processedGlobs = new();

                    foreach (CustomFileGlobModel customGlobModel in ConfigurationModel.SpecialCustomOperations)
                    {
                        if (customGlobModel.ConditionResult)
                        {
                            // only add the special if the condition is true
                            OperationConfigDefault defaultParams = defaultSpecials.FirstOrDefault(x => x.Glob == customGlobModel.Glob);

                            defaultParams ??= OperationConfigDefault.Default;

                            GlobalRunConfig runConfig = ProduceOperationSetup(defaultParams, false, customGlobModel);
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

                        GlobalRunConfig runConfig = ProduceOperationSetup(defaultParams, false, null);
                        specialOperationConfig.Add((defaultParams.Glob, runConfig));
                    }

                    _specialOperationConfig = specialOperationConfig;
                }

                return _specialOperationConfig;
            }
        }

        /// <summary>
        /// Gets the mount point where template sources are located.
        /// </summary>
        internal IMountPoint SourceMountPoint { get; }

        internal TemplateLocalizationInfo? Localization => _localizationInfo;

        internal IReadOnlyList<IReplacementTokens> SymbolFilenameReplacements
        {
            get
            {
                _symbolFilenameReplacements ??= ProduceSymbolFilenameReplacements();
                return _symbolFilenameReplacements;
            }
        }

        internal override IReadOnlyDictionary<CultureInfo, TemplateLocalizationInfo> Localizations =>
             _localizationInfo is null
                ? new Dictionary<CultureInfo, TemplateLocalizationInfo>()
                : new Dictionary<CultureInfo, TemplateLocalizationInfo>()
                {
                    { _localizationInfo.Locale, _localizationInfo }
                };

        internal override IReadOnlyDictionary<string, IFile> HostFiles =>
            _hostConfigFile is null
                ? new Dictionary<string, IFile>()
                : new Dictionary<string, IFile>()
                {
                    { ParseHostFileName(_hostConfigFile.Name), _hostConfigFile }
                };

        public void RemoveParameter(ITemplateParameter parameter)
        {
            ConfigurationModel.RemoveSymbol(parameter.Name);
        }

        /// <summary>
        /// Evaluates the conditions in template configurations: conditions in special custom operations, in sources and primary outputs.
        /// File renames are also applied at this step.
        /// </summary>
        /// <param name="rootVariableCollection"></param>
        public void Evaluate(IVariableCollection rootVariableCollection)
        {
            // evaluate the file glob (specials) conditions
            // the result is needed for SpecialOperationConfig
            foreach (CustomFileGlobModel fileGlobModel in ConfigurationModel.SpecialCustomOperations)
            {
                fileGlobModel.EvaluateCondition(Logger, rootVariableCollection);
            }

            rootVariableCollection.TryGetValue(ConfigurationModel.NameSymbol.Name, out object? resolvedNameParamValue);

            _sources = EvaluateSources(rootVariableCollection, resolvedNameParamValue);

            FileRenameGenerator renameGenerator = new(
                EngineEnvironmentSettings,
                ConfigurationModel.SourceName,
                resolvedNameParamValue,
                rootVariableCollection,
                SymbolFilenameReplacements);

            _evaluatedPrimaryOutputs = PrimaryOutput.Evaluate(
                EngineEnvironmentSettings,
                ConfigurationModel.PrimaryOutputs,
                rootVariableCollection,
                renameGenerator);

            _evaluatedPostActions = PostAction.Evaluate(
                EngineEnvironmentSettings,
                ConfigurationModel.PostActionModels,
                rootVariableCollection,
                renameGenerator);
        }

        /// <summary>
        /// Evaluates the bind symbols.
        /// The evaluated bind symbols are written to <paramref name="variableCollection"/>.
        /// </summary>
        public Task EvaluateBindSymbolsAsync(IEngineEnvironmentSettings settings, IVariableCollection variableCollection, CancellationToken cancellationToken)
        {
            IEnumerable<BindSymbol> bindSymbols = ConfigurationModel.Symbols.OfType<BindSymbol>();
            if (!bindSymbols.Any())
            {
                return Task.FromResult(0);
            }
            BindSymbolEvaluator bindSymbolEvaluator = new BindSymbolEvaluator(settings);

            return bindSymbolEvaluator.EvaluateBindSymbolsAsync(bindSymbols, variableCollection, cancellationToken);
        }

        internal void Localize() => ConfigurationModel.Localize(Localizations.Single().Value.Model);

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
                                EngineEnvironmentSettings.Host.Logger.LogWarning(LocalizableStrings.RunnableProjectConfig_OperationSetup_UnknownForm, p.Name, formName);
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

        private GlobalRunConfig ProduceOperationSetup(OperationConfigDefault defaultModel, bool generateMacros, CustomFileGlobModel? customGlobModel = null)
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

            IVariableConfig variableConfig = customGlobModel != null ? customGlobModel.VariableFormat : VariableConfig.Default;

            List<IReplacementTokens> macroGeneratedReplacements = new();

            List<IMacroConfig> macros = new();

            if (generateMacros)
            {
                macros.AddRange(ProduceGeneratedSymbolsMacroConfig());
                macros.AddRange(ProduceComputedMacroConfig());
            }

            foreach (BaseSymbol symbol in ConfigurationModel.Symbols)
            {
                if (symbol is DerivedSymbol derivedSymbol && generateMacros)
                {
                    if (ConfigurationModel.Forms.TryGetValue(derivedSymbol.ValueTransform, out _))
                    {
                        macros.Add(new ProcessValueFormMacroConfig(derivedSymbol.ValueSource, symbol.Name, derivedSymbol.DataType, derivedSymbol.ValueTransform, ConfigurationModel.Forms));
                    }
                    else
                    {
                        EngineEnvironmentSettings.Host.Logger.LogWarning(LocalizableStrings.RunnableProjectConfig_OperationSetup_UnknownForm, derivedSymbol.Name, derivedSymbol.ValueTransform);
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
                                    macros.Add(new ProcessValueFormMacroConfig(sourceVariable, symbolName, "string", formName, ConfigurationModel.Forms));
                                }
                            }
                            else
                            {
                                EngineEnvironmentSettings.Host.Logger.LogWarning(LocalizableStrings.RunnableProjectConfig_OperationSetup_UnknownForm, baseValueSymbol.Name, formName);
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

            GlobalRunConfig config = new()
            {
                Operations = operations,
                VariableSetup = variableConfig,
                Macros = macros,
                SymbolNames = ConfigurationModel.Symbols.Select(x => x.Name).ToList(),
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

        private List<IMacroConfig> ProduceGeneratedSymbolsMacroConfig()
        {
            var generatedSymbolsConfigs = ConfigurationModel.Symbols.OfType<IGeneratedSymbolConfig>().ToList();
            Dictionary<string, IGeneratedSymbolMacro> generatedSymbolMacros = EngineEnvironmentSettings.Components.OfType<IGeneratedSymbolMacro>()
                .ToDictionary(m => m.Type, m => m);

            var generatedMacroConfigs = new List<IMacroConfig>();
            foreach (var generatedSymbolConfig in generatedSymbolsConfigs)
            {
                if (generatedSymbolMacros.TryGetValue(generatedSymbolConfig.Type, out var generatedSymbolMacro))
                {
                    generatedMacroConfigs.Add(generatedSymbolMacro.CreateConfig(EngineEnvironmentSettings, generatedSymbolConfig));
                }
                else
                {
                    EngineEnvironmentSettings.Host.Logger.LogWarning(
                        LocalizableStrings.MacroProcessor_Warning_UnknownMacro,
                        generatedSymbolConfig.VariableName,
                        generatedSymbolConfig.Type);
                }
            }

            return generatedMacroConfigs;
        }

        private List<BaseMacroConfig> ProduceComputedMacroConfig()
        {
            List<BaseMacroConfig> computedMacroConfigs = new();
            foreach (ComputedSymbol symbol in ConfigurationModel.Symbols.OfType<ComputedSymbol>())
            {
                string value = symbol.Value;
                string? evaluator = symbol.Evaluator;
                computedMacroConfigs.Add(new EvaluateMacroConfig(symbol.Name, "bool", value, evaluator));
            }

            if (ConfigurationModel.Guids != null)
            {
                int guidCount = 0;
                foreach (Guid guid in ConfigurationModel.Guids)
                {
                    int id = guidCount++;
                    string replacementId = "guid" + id;
                    computedMacroConfigs.Add(new GuidMacroConfig(replacementId, "string", null, null));
                    _guidToGuidPrefixMap[guid] = replacementId;
                }
            }
            return computedMacroConfigs;
        }

        private List<FileSourceMatchInfo> EvaluateSources(IVariableCollection rootVariableCollection, object? resolvedNameParamValue)
        {
            List<FileSourceMatchInfo> sources = new List<FileSourceMatchInfo>();

            foreach (ExtendedFileSource source in ConfigurationModel.Sources)
            {
                if (!source.EvaluateCondition(Logger, rootVariableCollection))
                {
                    continue;
                }

                IReadOnlyList<string> topIncludePattern = TryReadConfigFromFile(source.Include, ConfigFile, IncludePatternDefaults).ToList();
                IReadOnlyList<string> topExcludePattern = TryReadConfigFromFile(source.Exclude, ConfigFile, ExcludePatternDefaults).ToList();
                IReadOnlyList<string> topCopyOnlyPattern = TryReadConfigFromFile(source.CopyOnly, ConfigFile, CopyOnlyPatternDefaults).ToList();
                FileSourceEvaluable topLevelPatterns = new FileSourceEvaluable(topIncludePattern, topExcludePattern, topCopyOnlyPattern);

                Dictionary<string, string> fileRenamesFromSource = source.Rename.ToDictionary(x => x.Key, x => x.Value);
                List<FileSourceEvaluable> modifierList = new List<FileSourceEvaluable>();

                if (source.Modifiers != null)
                {
                    foreach (SourceModifier modifier in source.Modifiers)
                    {
                        if (modifier.EvaluateCondition(Logger, rootVariableCollection))
                        {
                            IReadOnlyList<string> modifierIncludes = TryReadConfigFromFile(modifier.Include, ConfigFile, Array.Empty<string>());
                            IReadOnlyList<string> modifierExcludes = TryReadConfigFromFile(modifier.Exclude, ConfigFile, Array.Empty<string>());
                            IReadOnlyList<string> modifierCopyOnly = TryReadConfigFromFile(modifier.CopyOnly, ConfigFile, Array.Empty<string>());
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
            => FileRenameGenerator.AugmentFileRenames(EngineEnvironmentSettings, ConfigurationModel.SourceName, TemplateSourceRoot, sourceDirectory, ref targetDirectory, resolvedNameParamValue, variables, fileRenames, SymbolFilenameReplacements);
    }
}
