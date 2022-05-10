// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// The class maps template.json configuration read in <see cref="SimpleConfigModel"/> to runnable configuration.
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
        private static readonly Dictionary<string, string> RenameDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly string[] DefaultPlaceholderFilenames = new[] { "-.-", "_._" };
        private readonly IEngineEnvironmentSettings _settings;
        private readonly ILogger<RunnableProjectConfig> _logger;

        private readonly SimpleConfigModel _configuration;
        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new Dictionary<Guid, string>();

        private readonly IGenerator _generator;
        private readonly IFile? _sourceFile;
        private readonly IFile? _localeConfigFile;
        private readonly IFile? _hostConfigFile;

        private IReadOnlyList<FileSourceMatchInfo>? _sources;
        private IGlobalRunConfig? _operationConfig;
        private IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>>? _specialOperationConfig;
        private IReadOnlyList<IReplacementTokens>? _symbolFilenameReplacements;
        private IReadOnlyDictionary<string, Parameter>? _parameters;

        internal RunnableProjectConfig(IEngineEnvironmentSettings settings, IGenerator generator, IFile templateFile, IFile? hostConfigFile = null, IFile? localeConfigFile = null, string? baselineName = null)
        {
            _settings = settings;
            _logger = _settings.Host.LoggerFactory.CreateLogger<RunnableProjectConfig>();
            _generator = generator;
            _sourceFile = templateFile;
            _hostConfigFile = hostConfigFile;
            _localeConfigFile = localeConfigFile;

            ISimpleConfigModifiers? configModifiers = null;
            if (!string.IsNullOrWhiteSpace(baselineName))
            {
                configModifiers = new SimpleConfigModifiers(baselineName!);
            }
            _configuration = SimpleConfigModel.FromJObject (
                MergeAdditionalConfiguration(templateFile.ReadJObjectFromIFile(), templateFile),
                _settings.Host.LoggerFactory.CreateLogger<SimpleConfigModel>(),
                configModifiers,
                templateFile.GetDisplayPath());

            CheckGeneratorVersionRequiredByTemplate();
            PerformTemplateValidation();
            Identity = _configuration.Identity!;

            if (_localeConfigFile != null)
            {
                try
                {
                    ILocalizationModel locModel = LocalizationModelDeserializer.Deserialize(_localeConfigFile);
                    if (VerifyLocalizationModel(locModel))
                    {
                        _configuration.Localize(locModel);
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
        internal RunnableProjectConfig(IEngineEnvironmentSettings settings, IGenerator generator, SimpleConfigModel configuration, IFile? configurationFile = null)
        {
            _settings = settings;
            _logger = _settings.Host.LoggerFactory.CreateLogger<RunnableProjectConfig>();
            _generator = generator;
            _configuration = configuration;
            Identity = configuration.Identity ?? throw new ArgumentException($"{nameof(configuration)} should have identity set.");
            _sourceFile = configurationFile;
        }

        public string Identity { get; }

        public IReadOnlyList<PostActionModel> PostActionModels => _configuration.PostActionModels;

        public IReadOnlyList<ICreationPathModel> PrimaryOutputs => _configuration.PrimaryOutputs;

        public IFile SourceFile => _sourceFile ?? throw new InvalidOperationException("Source file is not initialized, are you using test constructor?");

        public IDirectory? TemplateSourceRoot => SourceFile.Parent?.Parent;

        public IReadOnlyList<string> IgnoreFileNames => !string.IsNullOrWhiteSpace(_configuration.PlaceholderFilename) ? new[] { _configuration.PlaceholderFilename! } : DefaultPlaceholderFilenames;

        public IReadOnlyList<FileSourceMatchInfo> Sources
        {
            get
            {
                if (_sources == null)
                {
                    if (SourceFile == null)
                    {
                        throw new NotSupportedException($"{nameof(SourceFile)} should be set for sources processing.");
                    }
                    List<FileSourceMatchInfo> sources = new List<FileSourceMatchInfo>();

                    foreach (ExtendedFileSource source in _configuration.Sources)
                    {
                        IReadOnlyList<string> includePattern = JTokenAsFilenameToReadOrArrayToCollection(source.Include, SourceFile, IncludePatternDefaults);
                        IReadOnlyList<string> excludePattern = JTokenAsFilenameToReadOrArrayToCollection(source.Exclude, SourceFile, ExcludePatternDefaults);
                        IReadOnlyList<string> copyOnlyPattern = JTokenAsFilenameToReadOrArrayToCollection(source.CopyOnly, SourceFile, CopyOnlyPatternDefaults);
                        FileSourceEvaluable topLevelEvaluable = new FileSourceEvaluable(includePattern, excludePattern, copyOnlyPattern);
                        IReadOnlyDictionary<string, string> renamePatterns = new Dictionary<string, string>(source.Rename ?? RenameDefaults, StringComparer.Ordinal);
                        FileSourceMatchInfo matchInfo = new FileSourceMatchInfo(
                            source.Source ?? "./",
                            source.Target ?? "./",
                            topLevelEvaluable,
                            renamePatterns,
                            new List<FileSourceEvaluable>());
                        sources.Add(matchInfo);
                    }

                    if (sources.Count == 0)
                    {
                        IReadOnlyList<string> includePattern = IncludePatternDefaults;
                        IReadOnlyList<string> excludePattern = ExcludePatternDefaults;
                        IReadOnlyList<string> copyOnlyPattern = CopyOnlyPatternDefaults;
                        FileSourceEvaluable topLevelEvaluable = new FileSourceEvaluable(includePattern, excludePattern, copyOnlyPattern);

                        FileSourceMatchInfo matchInfo = new FileSourceMatchInfo(
                            "./",
                            "./",
                            topLevelEvaluable,
                            new Dictionary<string, string>(StringComparer.Ordinal),
                            new List<FileSourceEvaluable>());
                        sources.Add(matchInfo);
                    }

                    _sources = sources;
                }

                return _sources;
            }
        }

        public IGlobalRunConfig OperationConfig
        {
            get
            {
                if (_operationConfig == null)
                {
                    SpecialOperationConfigParams defaultOperationParams = new SpecialOperationConfigParams(string.Empty, "//", "C++", ConditionalType.CLineComments);
                    _operationConfig = ProduceOperationSetup(defaultOperationParams, true, _configuration.GlobalCustomOperations);
                }

                return _operationConfig;
            }
        }

        public IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> SpecialOperationConfig
        {
            get
            {
                if (_specialOperationConfig == null)
                {
                    List<SpecialOperationConfigParams> defaultSpecials = new List<SpecialOperationConfigParams>
                    {
                        new SpecialOperationConfigParams("**/*.js", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.es", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.es6", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.ts", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.json", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.jsonld", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.hjson", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.json5", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.geojson", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.topojson", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.bowerrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.npmrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.job", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.postcssrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.babelrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.csslintrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.eslintrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.jade-lintrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.pug-lintrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.jshintrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.stylelintrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.yarnrc", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.css.min", "/*", "C++", ConditionalType.CBlockComments),
                        new SpecialOperationConfigParams("**/*.css", "/*", "C++", ConditionalType.CBlockComments),
                        new SpecialOperationConfigParams("**/*.cshtml", "@*", "C++", ConditionalType.Razor),
                        new SpecialOperationConfigParams("**/*.razor", "@*", "C++", ConditionalType.Razor),
                        new SpecialOperationConfigParams("**/*.vbhtml", "@*", "VB", ConditionalType.Razor),
                        new SpecialOperationConfigParams("**/*.cs", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.fs", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.c", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.cpp", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.cxx", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.h", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.hpp", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.hxx", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.cake", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.*proj", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.*proj.user", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.pubxml", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.pubxml.user", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.msbuild", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.targets", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.props", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.svg", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.*htm", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.*html", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.md", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.jsp", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.asp", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.aspx", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/app.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/web.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/web.*.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/packages.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/nuget.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.nuspec", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.xslt", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.xsd", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.vsixmanifest", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.vsct", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.storyboard", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.axml", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.plist", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.xib", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.strings", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.bat", "rem --:", "C++", ConditionalType.RemLineComment),
                        new SpecialOperationConfigParams("**/*.cmd", "rem --:", "C++", ConditionalType.RemLineComment),
                        new SpecialOperationConfigParams("**/nginx.conf", "#--", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/robots.txt", "#--", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/*.sh", "#--", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/*.haml", "-#", "C++", ConditionalType.HamlLineComment),
                        new SpecialOperationConfigParams("**/*.jsx", "{/*", "C++", ConditionalType.JsxBlockComment),
                        new SpecialOperationConfigParams("**/*.tsx", "{/*", "C++", ConditionalType.JsxBlockComment),
                        new SpecialOperationConfigParams("**/*.xml", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.resx", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.bas", "'", "VB", ConditionalType.VB),
                        new SpecialOperationConfigParams("**/*.vb", "'", "VB", ConditionalType.VB),
                        new SpecialOperationConfigParams("**/*.xaml", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.sln", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/*.yaml", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/*.yml", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/Dockerfile", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/.editorconfig", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/.gitattributes", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/.gitignore", "#-", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/.dockerignore", "#-", "C++", ConditionalType.HashSignLineComment)
                    };
                    List<KeyValuePair<string, IGlobalRunConfig>> specialOperationConfig = new List<KeyValuePair<string, IGlobalRunConfig>>();

                    // put the custom configs first in the list
                    HashSet<string> processedGlobs = new HashSet<string>();

                    foreach (ICustomFileGlobModel customGlobModel in _configuration.SpecialCustomOperations)
                    {
                        if (customGlobModel.ConditionResult)
                        {
                            // only add the special if the condition is true
                            SpecialOperationConfigParams defaultParams = defaultSpecials.Where(x => x.Glob == customGlobModel.Glob).FirstOrDefault();

                            if (defaultParams == null)
                            {
                                defaultParams = SpecialOperationConfigParams.Defaults;
                            }

                            IGlobalRunConfig runConfig = ProduceOperationSetup(defaultParams, false, customGlobModel);
                            specialOperationConfig.Add(new KeyValuePair<string, IGlobalRunConfig>(customGlobModel.Glob, runConfig));
                        }

                        // mark this special as already processed, so it doesn't get included with the defaults
                        // even if the special was skipped due to its custom condition.
                        processedGlobs.Add(customGlobModel.Glob);
                    }

                    // add the remaining default configs in the order specified above
                    foreach (SpecialOperationConfigParams defaultParams in defaultSpecials)
                    {
                        if (processedGlobs.Contains(defaultParams.Glob))
                        {
                            // this one was already setup due to a custom config
                            continue;
                        }

                        IGlobalRunConfig runConfig = ProduceOperationSetup(defaultParams, false, null);
                        specialOperationConfig.Add(new KeyValuePair<string, IGlobalRunConfig>(defaultParams.Glob, runConfig));
                    }

                    _specialOperationConfig = specialOperationConfig;
                }

                return _specialOperationConfig;
            }
        }

        public IReadOnlyDictionary<string, Parameter> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter>();
                    foreach (KeyValuePair<string, ISymbolModel> symbol in _configuration.Symbols)
                    {
                        if (!string.Equals(symbol.Value.Type, ParameterSymbol.TypeName, StringComparison.Ordinal) &&
                                !string.Equals(symbol.Value.Type, DerivedSymbol.TypeName, StringComparison.Ordinal) ||
                                !(symbol.Value is BaseValueSymbol baseSymbol))
                        {
                            // Symbol is of wrong type. Skip.
                            continue;
                        }

                        bool isName = baseSymbol == _configuration.NameSymbol;

                        Parameter parameter = new Parameter
                        {
                            DefaultValue = baseSymbol.DefaultValue ?? (!baseSymbol.IsRequired ? baseSymbol.Replaces : null),
                            IsName = isName,
                            IsVariable = true,
                            Name = symbol.Key,
                            Priority = baseSymbol.IsRequired ? TemplateParameterPriority.Required : isName ? TemplateParameterPriority.Implicit : TemplateParameterPriority.Optional,
                            Type = baseSymbol.Type,
                            DataType = baseSymbol.DataType
                        };

                        if (string.Equals(symbol.Value.Type, ParameterSymbol.TypeName, StringComparison.Ordinal) &&
                                symbol.Value is ParameterSymbol parameterSymbol)
                        {
                            parameter.Priority = parameterSymbol.IsTag ? TemplateParameterPriority.Implicit : parameter.Priority;
                            parameter.Description = parameterSymbol.Description;
                            parameter.Choices = parameterSymbol.Choices;
                            parameter.DefaultIfOptionWithoutValue = parameterSymbol.DefaultIfOptionWithoutValue;
                            parameter.DisplayName = parameterSymbol.DisplayName;
                        }

                        parameters[symbol.Key] = parameter;
                    }

                    _parameters = parameters;
                }
                return _parameters;
            }
        }

        internal Parameter NameParameter => Parameters.Values.First(p => p.IsName);

        internal IReadOnlyList<IReplacementTokens> SymbolFilenameReplacements
        {
            get
            {
                if (_symbolFilenameReplacements == null)
                {
                    _symbolFilenameReplacements = ProduceSymbolFilenameReplacements();
                }
                return _symbolFilenameReplacements;
            }
        }

        internal SimpleConfigModel ConfigurationModel => _configuration;

        public void Evaluate(IParameterSet parameters, IVariableCollection rootVariableCollection)
        {
            bool stable = false;
            Dictionary<string, bool> computed = new Dictionary<string, bool>();

            while (!stable)
            {
                stable = true;
                foreach (KeyValuePair<string, ISymbolModel> symbol in _configuration.Symbols)
                {
                    if (string.Equals(symbol.Value.Type, ComputedSymbol.TypeName, StringComparison.Ordinal))
                    {
                        ComputedSymbol sym = (ComputedSymbol)symbol.Value;
                        bool value = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_settings, sym.Value, rootVariableCollection);
                        stable &= computed.TryGetValue(symbol.Key, out bool currentValue) && currentValue == value;
                        rootVariableCollection[symbol.Key] = value;
                        computed[symbol.Key] = value;
                    }
                    else if (string.Equals(symbol.Value.Type, SymbolModelConverter.BindSymbolTypeName, StringComparison.Ordinal))
                    {
                        if (parameters.TryGetRuntimeValue(_settings, symbol.Value.Binding, out object bindValue) && bindValue != null)
                        {
                            rootVariableCollection[symbol.Key] = RunnableProjectGenerator.InferTypeAndConvertLiteral(bindValue.ToString());
                        }
                    }
                }
            }

            // evaluate the file glob (specials) conditions
            // the result is needed for SpecialOperationConfig
            foreach (ICustomFileGlobModel fileGlobModel in _configuration.SpecialCustomOperations)
            {
                fileGlobModel.EvaluateCondition(_settings, rootVariableCollection);
            }

            parameters.ResolvedValues.TryGetValue(NameParameter, out object resolvedNameParamValue);

            _sources = EvaluateSources(parameters, rootVariableCollection, resolvedNameParamValue);

            // evaluate the conditions and resolve the paths for the PrimaryOutputs
            foreach (ICreationPathModel pathModel in _configuration.PrimaryOutputs)
            {
                pathModel.EvaluateCondition(_settings, rootVariableCollection);

                if (pathModel.ConditionResult)
                {
                    pathModel.PathResolved = FileRenameGenerator.ApplyRenameToPrimaryOutput(
                        pathModel.PathOriginal,
                        _settings,
                        _configuration.SourceName,
                        resolvedNameParamValue,
                        parameters,
                        SymbolFilenameReplacements);
                }
            }
        }

        /// <summary>
        /// Verifies that the given localization model was correctly constructed
        /// to localize this SimpleConfigModel.
        /// </summary>
        /// <param name="locModel">The localization model to be verified.</param>
        /// <param name="localeFile">localization file (optional), needed to get file name for logging.</param>
        /// <returns>True if the verification succeeds. False otherwise.
        /// Check logs for details in case of a failed verification.</returns>
        internal bool VerifyLocalizationModel(ILocalizationModel locModel, IFile? localeFile = null)
        {
            bool validModel = true;
            localeFile = localeFile ?? _localeConfigFile;
            List<string> errorMessages = new List<string>();
            int unusedPostActionLocs = locModel.PostActions.Count;
            foreach (var postAction in PostActionModels)
            {
                if (postAction.Id == null || !locModel.PostActions.TryGetValue(postAction.Id, out IPostActionLocalizationModel postActionLocModel))
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
                string excessPostActionLocalizationIds = string.Join(", ", locModel.PostActions.Keys.Where(k => !PostActionModels.Any(p => p.Id == k)).Select(k => k.ToString()));
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

                IFile partialConfigFile = primarySourceConfig.Parent.EnumerateFiles(partialConfigFileName, SearchOption.TopDirectoryOnly).FirstOrDefault(x => string.Equals(x.Name, partialConfigFileName));

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
        private static IReadOnlyList<string> JTokenAsFilenameToReadOrArrayToCollection(JToken token, IFile sourceFile, string[] defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.Type == JTokenType.String)
            {
                string tokenValue = token.ToString();
                if ((tokenValue.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                    || !sourceFile.Parent.FileInfo(tokenValue).Exists)
                {
                    return new List<string>(new[] { tokenValue });
                }
                else
                {
                    using (Stream excludeList = sourceFile.Parent.FileInfo(token.ToString()).OpenRead())
                    using (TextReader reader = new StreamReader(excludeList, Encoding.UTF8, true, 4096, true))
                    {
                        return reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                }
            }

            return token.ArrayAsStrings();
        }

        private IReadOnlyList<IReplacementTokens> ProduceSymbolFilenameReplacements()
        {
            List<IReplacementTokens> filenameReplacements = new List<IReplacementTokens>();
            if (_configuration.Symbols.Count == 0)
            {
                return filenameReplacements;
            }

            foreach (KeyValuePair<string, ISymbolModel> symbol in _configuration.Symbols.Where(s => !string.IsNullOrWhiteSpace(s.Value.FileRename)))
            {
                if (symbol.Value is BaseValueSymbol p)
                {
                    foreach (string formName in p.Forms.GlobalForms)
                    {
                        if (_configuration.Forms.TryGetValue(formName, out IValueForm valueForm))
                        {
                            string symbolName = symbol.Key + "{-VALUE-FORMS-}" + formName;
                            string processedFileReplacement = valueForm.Process(_configuration.Forms, p.FileRename);
                            GenerateFileReplacementsForSymbol(processedFileReplacement, symbolName, filenameReplacements);
                        }
                        else
                        {
                            _settings.Host.Logger.LogDebug($"Unable to find a form called '{formName}'");
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(symbol.Value.FileRename))
                    {
                        GenerateFileReplacementsForSymbol(symbol.Value.FileRename!, symbol.Key, filenameReplacements);
                    }
                }
            }
            return filenameReplacements;
        }

        private IGlobalRunConfig ProduceOperationSetup(SpecialOperationConfigParams defaultModel, bool generateMacros, ICustomFileGlobModel? customGlobModel = null)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();

            // TODO: if we allow custom config to specify a built-in conditional type, decide what to do.
            if (defaultModel.ConditionalStyle != ConditionalType.None)
            {
                operations.AddRange(ConditionalConfig.ConditionalSetup(defaultModel.ConditionalStyle, defaultModel.EvaluatorName, true, true, null));
            }

            if (customGlobModel == null || string.IsNullOrEmpty(customGlobModel.FlagPrefix))
            {
                // these conditions may need to be separated - if there is custom info, but the flag prefix was not provided, we might want to raise a warning / error
                operations.AddRange(FlagsConfig.FlagsDefaultSetup(defaultModel.FlagPrefix));
            }
            else
            {
                operations.AddRange(FlagsConfig.FlagsDefaultSetup(customGlobModel.FlagPrefix));
            }

            IVariableConfig variableConfig;
            if (customGlobModel != null)
            {
                variableConfig = customGlobModel.VariableFormat;
            }
            else
            {
                variableConfig = VariableConfig.DefaultVariableSetup();
            }

            List<IMacroConfig>? macros = null;
            List<IMacroConfig> computedMacros = new List<IMacroConfig>();
            List<IReplacementTokens> macroGeneratedReplacements = new List<IReplacementTokens>();

            if (generateMacros)
            {
                macros = ProduceMacroConfig(computedMacros);
            }

            foreach (KeyValuePair<string, ISymbolModel> symbol in _configuration.Symbols)
            {
                if (symbol.Value is DerivedSymbol derivedSymbol)
                {
                    if (generateMacros)
                    {
                        macros?.Add(new ProcessValueFormMacroConfig(derivedSymbol.ValueSource, symbol.Key, derivedSymbol.DataType, derivedSymbol.ValueTransform, _configuration.Forms));
                    }
                }

                string? sourceVariable = null;
                if (string.Equals(symbol.Value.Type, SymbolModelConverter.BindSymbolTypeName, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(symbol.Value.Binding))
                    {
                        _settings.Host.Logger.LogWarning($"Binding wasn't specified for bind-type symbol {symbol.Key}");
                    }
                    else
                    {
                        //Since this is a bind symbol, don't replace the literal with this symbol's value,
                        //  replace it with the value of the bound symbol
                        sourceVariable = symbol.Value.Binding;
                    }
                }
                else
                {
                    //Replace the literal value in the "replaces" property with the evaluated value of the symbol
                    sourceVariable = symbol.Key;
                }

                if (sourceVariable != null)
                {
                    if (symbol.Value is BaseValueSymbol p && p.Forms != null)
                    {
                        foreach (string formName in p.Forms.GlobalForms)
                        {
                            if (_configuration.Forms.TryGetValue(formName, out IValueForm valueForm))
                            {
                                string symbolName = sourceVariable + "{-VALUE-FORMS-}" + formName;
                                if (!string.IsNullOrWhiteSpace(symbol.Value.Replaces))
                                {
                                    string processedReplacement = valueForm.Process(_configuration.Forms, p.Replaces);
                                    GenerateReplacementsForParameter(symbol, processedReplacement, symbolName, macroGeneratedReplacements);
                                }
                                if (generateMacros)
                                {
                                    macros?.Add(new ProcessValueFormMacroConfig(sourceVariable, symbolName, "string", formName, _configuration.Forms));
                                }
                            }
                            else
                            {
                                _settings.Host.Logger.LogDebug($"Unable to find a form called '{formName}'");
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(symbol.Value.Replaces))
                    {
                        GenerateReplacementsForParameter(symbol, symbol.Value.Replaces!, sourceVariable, macroGeneratedReplacements);
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

            IReadOnlyList<ICustomOperationModel> customOperationConfig;
            if (customGlobModel != null && customGlobModel.Operations != null)
            {
                customOperationConfig = customGlobModel.Operations;
            }
            else
            {
                customOperationConfig = new List<ICustomOperationModel>();
            }

            foreach (IOperationProvider p in operations.ToList())
            {
                if (!string.IsNullOrEmpty(p.Id))
                {
                    string prefix = (customGlobModel == null || string.IsNullOrEmpty(customGlobModel.FlagPrefix)) ? defaultModel.FlagPrefix : customGlobModel.FlagPrefix;
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
                Macros = macros,
                ComputedMacros = computedMacros,
                Replacements = macroGeneratedReplacements,
                CustomOperations = customOperationConfig
            };
            return config;
        }

        private void GenerateReplacementsForParameter(KeyValuePair<string, ISymbolModel> symbol, string replaces, string sourceVariable, List<IReplacementTokens> macroGeneratedReplacements)
        {
            TokenConfig replacementConfig = replaces.TokenConfigBuilder();
            if (symbol.Value.ReplacementContexts.Count > 0)
            {
                foreach (IReplacementContext context in symbol.Value.ReplacementContexts)
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

        private List<IMacroConfig> ProduceMacroConfig(List<IMacroConfig> computedMacroConfigs)
        {
            List<IMacroConfig> generatedMacroConfigs = new List<IMacroConfig>();

            if (_configuration.Guids != null)
            {
                int guidCount = 0;
                foreach (Guid guid in _configuration.Guids)
                {
                    int id = guidCount++;
                    string replacementId = "guid" + id;
                    generatedMacroConfigs.Add(new GuidMacroConfig(replacementId, "string", null, null));
                    _guidToGuidPrefixMap[guid] = replacementId;
                }
            }

            foreach (KeyValuePair<string, ISymbolModel> symbol in _configuration.Symbols)
            {
                if (string.Equals(symbol.Value.Type, ComputedSymbol.TypeName, StringComparison.Ordinal))
                {
                    ComputedSymbol computed = (ComputedSymbol)symbol.Value;
                    string value = computed.Value;
                    string evaluator = computed.Evaluator;
                    string dataType = computed.DataType;
                    computedMacroConfigs.Add(new EvaluateMacroConfig(symbol.Key, dataType, value, evaluator));
                }
                else if (string.Equals(symbol.Value.Type, GeneratedSymbol.TypeName, StringComparison.Ordinal))
                {
                    GeneratedSymbol symbolInfo = (GeneratedSymbol)symbol.Value;
                    string type = symbolInfo.Generator;
                    string variableName = symbol.Key;
                    Dictionary<string, JToken> configParams = new Dictionary<string, JToken>();

                    foreach (KeyValuePair<string, JToken> parameter in symbolInfo.Parameters)
                    {
                        configParams.Add(parameter.Key, parameter.Value);
                    }

                    string dataType = symbolInfo.DataType;

                    if (string.Equals(dataType, "choice", StringComparison.OrdinalIgnoreCase))
                    {
                        dataType = "string";
                    }

                    generatedMacroConfigs.Add(new GeneratedSymbolDeferredMacroConfig(type, dataType, variableName, configParams));
                }
            }

            return generatedMacroConfigs;
        }

        private List<FileSourceMatchInfo> EvaluateSources(IParameterSet parameters, IVariableCollection rootVariableCollection, object resolvedNameParamValue)
        {
            if (SourceFile == null)
            {
                throw new NotSupportedException($"{nameof(SourceFile)} should be set for sources processing.");
            }

            List<FileSourceMatchInfo> sources = new List<FileSourceMatchInfo>();

            foreach (ExtendedFileSource source in _configuration.Sources)
            {
                if (!string.IsNullOrEmpty(source.Condition) && !Cpp2StyleEvaluatorDefinition.EvaluateFromString(_settings, source.Condition, rootVariableCollection))
                {
                    continue;
                }

                IReadOnlyList<string> topIncludePattern = JTokenAsFilenameToReadOrArrayToCollection(source.Include, SourceFile, IncludePatternDefaults).ToList();
                IReadOnlyList<string> topExcludePattern = JTokenAsFilenameToReadOrArrayToCollection(source.Exclude, SourceFile, ExcludePatternDefaults).ToList();
                IReadOnlyList<string> topCopyOnlyPattern = JTokenAsFilenameToReadOrArrayToCollection(source.CopyOnly, SourceFile, CopyOnlyPatternDefaults).ToList();
                FileSourceEvaluable topLevelPatterns = new FileSourceEvaluable(topIncludePattern, topExcludePattern, topCopyOnlyPattern);

                Dictionary<string, string> fileRenamesFromSource = new Dictionary<string, string>(source.Rename ?? RenameDefaults, StringComparer.Ordinal);
                List<FileSourceEvaluable> modifierList = new List<FileSourceEvaluable>();

                if (source.Modifiers != null)
                {
                    foreach (SourceModifier modifier in source.Modifiers)
                    {
                        if (string.IsNullOrEmpty(modifier.Condition) || Cpp2StyleEvaluatorDefinition.EvaluateFromString(_settings, modifier.Condition, rootVariableCollection))
                        {
                            IReadOnlyList<string> modifierIncludes = JTokenAsFilenameToReadOrArrayToCollection(modifier.Include, SourceFile, Array.Empty<string>());
                            IReadOnlyList<string> modifierExcludes = JTokenAsFilenameToReadOrArrayToCollection(modifier.Exclude, SourceFile, Array.Empty<string>());
                            IReadOnlyList<string> modifierCopyOnly = JTokenAsFilenameToReadOrArrayToCollection(modifier.CopyOnly, SourceFile, Array.Empty<string>());
                            FileSourceEvaluable modifierPatterns = new FileSourceEvaluable(modifierIncludes, modifierExcludes, modifierCopyOnly);
                            modifierList.Add(modifierPatterns);

                            if (modifier.Rename != null)
                            {
                                foreach (JProperty property in modifier.Rename.Properties())
                                {
                                    fileRenamesFromSource[property.Name] = property.Value.Value<string>() ?? string.Empty;
                                }
                            }
                        }
                    }
                }

                string sourceDirectory = source.Source ?? "./";
                string targetDirectory = source.Target ?? "./";
                IReadOnlyDictionary<string, string> allRenamesForSource = AugmentRenames(SourceFile, sourceDirectory, ref targetDirectory, resolvedNameParamValue, parameters, fileRenamesFromSource);

                FileSourceMatchInfo sourceMatcher = new FileSourceMatchInfo(
                    sourceDirectory,
                    targetDirectory,
                    topLevelPatterns,
                    allRenamesForSource,
                    modifierList);
                sources.Add(sourceMatcher);
            }

            if (_configuration.Sources.Count == 0)
            {
                IReadOnlyList<string> includePattern = IncludePatternDefaults;
                IReadOnlyList<string> excludePattern = ExcludePatternDefaults;
                IReadOnlyList<string> copyOnlyPattern = CopyOnlyPatternDefaults;
                FileSourceEvaluable topLevelPatterns = new FileSourceEvaluable(includePattern, excludePattern, copyOnlyPattern);

                string targetDirectory = string.Empty;
                Dictionary<string, string> fileRenamesFromSource = new Dictionary<string, string>(StringComparer.Ordinal);
                IReadOnlyDictionary<string, string> allRenamesForSource = AugmentRenames(SourceFile, "./", ref targetDirectory, resolvedNameParamValue, parameters, fileRenamesFromSource);

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

        private IReadOnlyDictionary<string, string> AugmentRenames(IFileSystemInfo configFile, string sourceDirectory, ref string targetDirectory, object resolvedNameParamValue, IParameterSet parameters, Dictionary<string, string> fileRenames)
        {
            return FileRenameGenerator.AugmentFileRenames(_settings, _configuration.SourceName, configFile, sourceDirectory, ref targetDirectory, resolvedNameParamValue, parameters, fileRenames, SymbolFilenameReplacements);
        }

        private void CheckGeneratorVersionRequiredByTemplate()
        {
            if (string.IsNullOrWhiteSpace(_configuration.GeneratorVersions))
            {
                return;
            }

            string allowedGeneratorVersions = _configuration.GeneratorVersions!;

            if (!VersionStringHelpers.TryParseVersionSpecification(allowedGeneratorVersions, out IVersionSpecification versionChecker) || !versionChecker.CheckIfVersionIsValid(RunnableProjectGenerator.GeneratorVersion))
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
            if (string.IsNullOrWhiteSpace(_configuration.Identity))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "identity"));
            }

            if (string.IsNullOrWhiteSpace(_configuration.Name))
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "name"));
            }

            if ((_configuration.ShortNameList?.Count ?? 0) == 0)
            {
                errorMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "shortName"));
            }
            errorMessages.AddRange(ValidateTemplateSourcePaths());
            #endregion

            #region Warnings
            //TODO: the warning messages should be transferred to validate subcommand, as they are not useful for final user, but useful for template author.
            //https://github.com/dotnet/templating/issues/2623
            if (string.IsNullOrWhiteSpace(_configuration.SourceName))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "sourceName"));
            }

            if (string.IsNullOrWhiteSpace(_configuration.Author))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "author"));
            }

            if (string.IsNullOrWhiteSpace(_configuration.GroupIdentity))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "groupIdentity"));
            }

            if (string.IsNullOrWhiteSpace(_configuration.GeneratorVersions))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "generatorVersions"));
            }

            if (_configuration.Precedence == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "precedence"));
            }

            if ((_configuration.Classifications?.Count ?? 0) == 0)
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MissingValue, "classifications"));
            }

            if (_configuration.PostActionModels != null && _configuration.PostActionModels.Any(x => x.ManualInstructionInfo == null || x.ManualInstructionInfo.Count == 0))
            {
                warningMessages.Add(string.Format(LocalizableStrings.Authoring_MalformedPostActionManualInstructions));
            }
            #endregion

            if (warningMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateMissingCommonInformation, SourceFile.GetDisplayPath()));
                foreach (string message in warningMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                _logger.LogDebug(stringBuilder.ToString());
            }

            if (errorMessages.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format(LocalizableStrings.Authoring_TemplateNotInstalled, SourceFile.GetDisplayPath()));
                foreach (string message in errorMessages)
                {
                    stringBuilder.AppendLine("  " + message);
                }
                _logger.LogError(stringBuilder.ToString());
                throw new TemplateValidationException(string.Format(LocalizableStrings.RunnableProjectGenerator_Exception_TemplateValidationFailed, SourceFile.GetDisplayPath()));
            }
        }

        /// <summary>
        /// The method checks if all the sources are valid.
        /// Example: the source directory should exist, should not be a file, should be accessible via mount point.
        /// </summary>
        /// <returns>list of found errors.</returns>
#pragma warning disable SA1202 // Elements should be ordered by access
        internal IEnumerable<string> ValidateTemplateSourcePaths()
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            List<string> errors = new List<string>();
            if (TemplateSourceRoot == null)
            {
                errors.Add(LocalizableStrings.Authoring_TemplateRootOutsideInstallSource);
            }
            // check if any sources get out of the mount point
            foreach (FileSourceMatchInfo source in Sources)
            {
                try
                {
                    IFile file = TemplateSourceRoot.FileInfo(source.Source);
                    //template source root should not be a file
                    if (file?.Exists ?? false)
                    {
                        errors.Add(string.Format(LocalizableStrings.Authoring_SourceMustBeDirectory, source.Source));
                    }
                    else
                    {
                        IDirectory sourceRoot = TemplateSourceRoot.DirectoryInfo(source.Source);
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

        private class SpecialOperationConfigParams
        {
            private static readonly SpecialOperationConfigParams _Defaults = new SpecialOperationConfigParams(string.Empty, string.Empty, "C++", ConditionalType.None);

            internal SpecialOperationConfigParams(string glob, string flagPrefix, string evaluatorName, ConditionalType type)
            {
                EvaluatorName = evaluatorName;
                Glob = glob;
                FlagPrefix = flagPrefix;
                ConditionalStyle = type;
            }

            internal static SpecialOperationConfigParams Defaults
            {
                get
                {
                    return _Defaults;
                }
            }

            internal string Glob { get; }

            internal string EvaluatorName { get; }

            internal string FlagPrefix { get; }

            internal ConditionalType ConditionalStyle { get; }
        }
    }
}
