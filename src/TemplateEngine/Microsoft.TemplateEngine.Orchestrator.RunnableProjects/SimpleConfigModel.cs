// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SimpleConfigModel : IRunnableProjectConfig
    {
        private static readonly string NameSymbolName = "name";
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

        internal SimpleConfigModel()
        {
            Sources = new[] { new ExtendedFileSource() };
        }

        private IReadOnlyDictionary<string, Parameter> _parameters;
        private IReadOnlyList<FileSourceMatchInfo> _sources;
        private IGlobalRunConfig _operationConfig;
        private IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> _specialOperationConfig;
        private Parameter _nameParameter;

        public IFile SourceFile { get; set; }

        public string Author { get; set; }

        internal IReadOnlyList<string> Classifications { get; set; }

        public string DefaultName { get; set; }

        public string Description { get; set; }

        public string GroupIdentity { get; set; }

        public int Precedence { get; set; }

        internal IReadOnlyList<Guid> Guids { get; set; }

        public string Name { get; set; }

        public IReadOnlyList<string> ShortNameList { get; set; }

        internal string SourceName { get; set; }

        internal IReadOnlyList<ExtendedFileSource> Sources { get; set; }

        internal IReadOnlyDictionary<string, ISymbolModel> Symbols { get; set; }

        public IReadOnlyList<IPostActionModel> PostActionModel { get; set; }

        public IReadOnlyList<ICreationPathModel> PrimaryOutputs { get; set; }
        public string GeneratorVersions { get; set; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; set; }

        public bool HasScriptRunningPostActions { get; set; }

        internal DateTime? ConfigTimestampUtc { get; set; }

        private IReadOnlyDictionary<string, string> _tagsDeprecated;

        public IReadOnlyDictionary<string, ICacheTag> Tags
        {
            get
            {
                Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();

                foreach (KeyValuePair<string, ParameterSymbol> tagParameter in TagDetails)
                {
                    ParameterSymbol tag = tagParameter.Value;
                    string defaultValue = tag.DefaultValue;

                    if (string.IsNullOrEmpty(defaultValue)
                        && (tagParameter.Value.Choices.Count == 1))
                    {
                        defaultValue = tagParameter.Value.Choices.Keys.First();
                    }

                    ICacheTag cacheTag = new CacheTag(tag.DisplayName, tag.Description, tag.Choices, defaultValue, tag.DefaultIfOptionWithoutValue);
                    tags.Add(tagParameter.Key, cacheTag);
                }

                return tags;
            }
        }

        private IReadOnlyDictionary<string, ParameterSymbol> _tagDetails;

        // Converts "choice" datatype parameter symbols to tags
        internal IReadOnlyDictionary<string, ParameterSymbol> TagDetails
        {
            get
            {
                if (_tagDetails == null)
                {
                    Dictionary<string, ParameterSymbol> tempTagDetails = new Dictionary<string, ParameterSymbol>();

                    foreach (KeyValuePair<string, ISymbolModel> symbolInfo in Symbols)
                    {
                        ParameterSymbol tagSymbol = symbolInfo.Value as ParameterSymbol;

                        if (tagSymbol != null && tagSymbol.DataType == "choice")
                        {
                            tempTagDetails.Add(symbolInfo.Key, tagSymbol);
                        }
                    }

                    _tagDetails = tempTagDetails;
                }

                return _tagDetails;
            }
        }

        private IReadOnlyDictionary<string, ICacheParameter> _cacheParameters;

        // Converts non-choice parameter symbols to cache parameters
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters
        {
            get
            {
                if (_cacheParameters == null)
                {
                    Dictionary<string, ICacheParameter> cacheParameters = new Dictionary<string, ICacheParameter>();

                    foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                    {
                        if (symbol.Value is ParameterSymbol param && param.DataType != "choice")
                        {
                            ICacheParameter cacheParam = new CacheParameter(param.DataType, param.DefaultValue, param.DisplayName, param.Description, param.DefaultIfOptionWithoutValue);
                            cacheParameters.Add(symbol.Key, cacheParam);
                        }
                    }

                    _cacheParameters = cacheParameters;
                }

                return _cacheParameters;
            }
        }

        IReadOnlyList<string> IRunnableProjectConfig.Classifications => Classifications;

        internal IReadOnlyDictionary<string, IValueForm> Forms { get; private set; }

        public string Identity { get; set; }

        private static readonly string DefaultPlaceholderFilename = "-.-";

        private string _placeholderValue;
        private IReadOnlyList<string> _ignoreFileNames;
        private bool _isPlaceholderFileNameCustomized;

        internal string PlaceholderFilename
        {
            get
            {
                return _placeholderValue;
            }
            set
            {
                _placeholderValue = value ?? DefaultPlaceholderFilename;

                if (value != null)
                {
                    _isPlaceholderFileNameCustomized = true;
                }
            }
        }

        public IReadOnlyList<string> IgnoreFileNames
        {
            get { return _ignoreFileNames ?? (_isPlaceholderFileNameCustomized ? new[] { PlaceholderFilename } : new[] { PlaceholderFilename, "_._" }); }
            set
            {
                _ignoreFileNames = value;
            }
        }

        IReadOnlyDictionary<string, Parameter> IRunnableProjectConfig.Parameters
        {
            get
            {
                if (_parameters != null)
                {
                    return _parameters;
                }

                Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter>();

                if (Symbols == null)
                {
                    _parameters = parameters;
                    return _parameters;
                }

                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (!string.Equals(symbol.Value.Type, ParameterSymbol.TypeName, StringComparison.Ordinal) &&
                            !string.Equals(symbol.Value.Type, DerivedSymbol.TypeName, StringComparison.Ordinal) ||
                            !(symbol.Value is BaseValueSymbol baseSymbol))
                    {
                        // Symbol is of wrong type. Skip.
                        continue;
                    }

                    bool isName = baseSymbol.Binding == NameSymbolName;

                    Parameter parameter = new Parameter
                    {
                        DefaultValue = baseSymbol.DefaultValue ?? (!baseSymbol.IsRequired ? baseSymbol.Replaces : null),
                        IsName = isName,
                        IsVariable = true,
                        Name = symbol.Key,
#pragma warning disable 612,618
                        FileRename = baseSymbol.FileRename,
#pragma warning restore 612,618
                        Requirement = baseSymbol.IsRequired ? TemplateParameterPriority.Required : isName ? TemplateParameterPriority.Implicit : TemplateParameterPriority.Optional,
                        Type = baseSymbol.Type,
                        DataType = baseSymbol.DataType
                    };

                    if (string.Equals(symbol.Value.Type, ParameterSymbol.TypeName, StringComparison.Ordinal) &&
                            symbol.Value is ParameterSymbol parameterSymbol)
                    {
                        parameter.Description = parameterSymbol.Description;
                        parameter.Choices = parameterSymbol.Choices;
                        parameter.DefaultIfOptionWithoutValue = parameterSymbol.DefaultIfOptionWithoutValue;
                    }

                    parameters[symbol.Key] = parameter;
                }

                _parameters = parameters;
                return _parameters;
            }
        }

        private Parameter NameParameter
        {
            get
            {
                if (_nameParameter == null)
                {
                    IRunnableProjectConfig cfg = this;
                    foreach (Parameter p in cfg.Parameters.Values)
                    {
                        if (p.IsName)
                        {
                            _nameParameter = p;
                            break;
                        }
                    }
                }

                return _nameParameter;
            }
        }

        IReadOnlyList<FileSourceMatchInfo> IRunnableProjectConfig.Sources
        {
            get
            {
                if (_sources == null)
                {
                    List<FileSourceMatchInfo> sources = new List<FileSourceMatchInfo>();

                    foreach (ExtendedFileSource source in Sources)
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

        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new Dictionary<Guid, string>();

        // operation info read from the config
        private ICustomFileGlobModel _customOperations = new CustomFileGlobModel();

        IGlobalRunConfig IRunnableProjectConfig.OperationConfig
        {
            get
            {
                if (_operationConfig == null)
                {
                    SpecialOperationConfigParams defaultOperationParams = new SpecialOperationConfigParams(string.Empty, "//", "C++", ConditionalType.CLineComments);
                    _operationConfig = ProduceOperationSetup(defaultOperationParams, true, _customOperations);
                }

                return _operationConfig;
            }
        }

        private IReadOnlyList<IReplacementTokens> _symbolFilenameReplacements;
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

        private class SpecialOperationConfigParams
        {
            internal SpecialOperationConfigParams(string glob, string flagPrefix, string evaluatorName, ConditionalType type)
            {
                EvaluatorName = evaluatorName;
                Glob = glob;
                FlagPrefix = flagPrefix;
                ConditionalStyle = type;
            }

            internal string Glob { get; }

            internal string EvaluatorName { get; }

            internal string FlagPrefix { get; }

            internal ConditionalType ConditionalStyle { get; }

            internal string VariableFormat { get; set; }

            private static readonly SpecialOperationConfigParams _Defaults = new SpecialOperationConfigParams(string.Empty, string.Empty, "C++", ConditionalType.None);

            internal static SpecialOperationConfigParams Defaults
            {
                get
                {
                    return _Defaults;
                }
            }
        }

        // file -> replacements
        public IReadOnlyDictionary<string, IReadOnlyList<IOperationProvider>> LocalizationOperations { get; private set; }

        private IReadOnlyList<ICustomFileGlobModel> _specialCustomSetup = new List<ICustomFileGlobModel>();

        IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> IRunnableProjectConfig.SpecialOperationConfig
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
                        new SpecialOperationConfigParams("**/*.jsp", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.asp", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.aspx", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/app.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/web.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/web.*.config", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/packages.config", "<!--", "C++", ConditionalType.Xml),
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
                        new SpecialOperationConfigParams("**/*.sln", "#-", "C++", ConditionalType.HashSignLineComment)
                    };
                    List<KeyValuePair<string, IGlobalRunConfig>> specialOperationConfig = new List<KeyValuePair<string, IGlobalRunConfig>>();

                    // put the custom configs first in the list
                    HashSet<string> processedGlobs = new HashSet<string>();

                    foreach (ICustomFileGlobModel customGlobModel in _specialCustomSetup)
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

        internal IEngineEnvironmentSettings EnvironmentSettings { get; private set; }

        private IGlobalRunConfig ProduceOperationSetup(SpecialOperationConfigParams defaultModel, bool generateMacros, ICustomFileGlobModel customGlobModel = null)
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
                variableConfig = VariableConfig.DefaultVariableSetup(defaultModel.VariableFormat);
            }

            List<IMacroConfig> macros = null;
            List<IMacroConfig> computedMacros = new List<IMacroConfig>();
            List<IReplacementTokens> macroGeneratedReplacements = new List<IReplacementTokens>();

            if (generateMacros)
            {
                macros = ProduceMacroConfig(computedMacros);
            }

            if (Symbols != null)
            {
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (symbol.Value is DerivedSymbol derivedSymbol)
                    {
                        if (generateMacros)
                        {
                            macros.Add(new ProcessValueFormMacroConfig(derivedSymbol.ValueSource, symbol.Key, derivedSymbol.DataType, derivedSymbol.ValueTransform, Forms));
                        }
                    }

                    string sourceVariable = null;
                    if (string.Equals(symbol.Value.Type, SymbolModelConverter.BindSymbolTypeName, StringComparison.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(symbol.Value.Binding))
                        {
                            EnvironmentSettings.Host.LogMessage($"Binding wasn't specified for bind-type symbol {symbol.Key}");
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
                                if (Forms.TryGetValue(formName, out IValueForm valueForm))
                                {
                                    string symbolName = sourceVariable + "{-VALUE-FORMS-}" + formName;
                                    if (!string.IsNullOrWhiteSpace(symbol.Value.Replaces))
                                    {
                                        string processedReplacement = valueForm.Process(Forms, p.Replaces);
                                        GenerateReplacementsForParameter(symbol, processedReplacement, symbolName, macroGeneratedReplacements);
                                    }
                                    if (generateMacros)
                                    {
                                        macros.Add(new ProcessValueFormMacroConfig(sourceVariable, symbolName, "string", formName, Forms));
                                    }
                                }
                                else
                                {
                                    EnvironmentSettings.Host.LogDiagnosticMessage($"Unable to find a form called '{formName}'", "Authoring");
                                }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(symbol.Value.Replaces))
                        {
                            GenerateReplacementsForParameter(symbol, symbol.Value.Replaces, sourceVariable, macroGeneratedReplacements);
                        }
                    }
                }
            }

            foreach (KeyValuePair<Guid, string> map in _guidToGuidPrefixMap)
            {
                foreach (char format in GuidMacroConfig.DefaultFormats)
                {
                    string newGuid = char.IsUpper(format) ? map.Key.ToString(format.ToString()).ToUpperInvariant() : map.Key.ToString(format.ToString()).ToLowerInvariant();
                    macroGeneratedReplacements.Add(new ReplacementTokens(map.Value + "-" + format, newGuid.TokenConfig()));
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

        private IReadOnlyList<IReplacementTokens> ProduceSymbolFilenameReplacements()
        {
            List<IReplacementTokens> filenameReplacements = new List<IReplacementTokens>();
            if (Symbols == null || Symbols.Count == 0)
            {
                return filenameReplacements;
            }

            foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols.Where(s => !string.IsNullOrWhiteSpace(s.Value.FileRename)))
            {
                if (symbol.Value is BaseValueSymbol p)
                {
                    foreach (string formName in p.Forms.GlobalForms)
                    {
                        if (Forms.TryGetValue(formName, out IValueForm valueForm))
                        {
                            string symbolName = symbol.Key + "{-VALUE-FORMS-}" + formName;
                            string processedFileReplacement = valueForm.Process(Forms, p.FileRename);
                            GenerateFileReplacementsForSymbol(processedFileReplacement, symbolName, filenameReplacements);
                        }
                        else
                        {
                            EnvironmentSettings.Host.LogDiagnosticMessage($"Unable to find a form called '{formName}'", "Authoring");
                        }
                    }
                }
                else
                {
                    GenerateFileReplacementsForSymbol(symbol.Value.FileRename, symbol.Key, filenameReplacements);
                }
            }
            return filenameReplacements;
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

            if (Guids != null)
            {
                int guidCount = 0;
                foreach (Guid guid in Guids)
                {
                    int id = guidCount++;
                    string replacementId = "guid" + id;
                    generatedMacroConfigs.Add(new GuidMacroConfig(replacementId, "string", null, null));
                    _guidToGuidPrefixMap[guid] = replacementId;
                }
            }

            if (Symbols != null)
            {
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
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
            }

            return generatedMacroConfigs;
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

        private static IReadOnlyList<string> JTokenStringOrArrayToCollection(JToken token, string[] defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.Type == JTokenType.String)
            {
                string tokenValue = token.ToString();
                return new List<string>() { tokenValue };
            }

            return token.ArrayAsStrings();
        }

        public void Evaluate(IParameterSet parameters, IVariableCollection rootVariableCollection, IFileSystemInfo configFile)
        {
            bool stable = Symbols == null;
            Dictionary<string, bool> computed = new Dictionary<string, bool>();

            while (!stable)
            {
                stable = true;
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (string.Equals(symbol.Value.Type, ComputedSymbol.TypeName, StringComparison.Ordinal))
                    {
                        ComputedSymbol sym = (ComputedSymbol)symbol.Value;
                        bool value = Cpp2StyleEvaluatorDefinition.EvaluateFromString(EnvironmentSettings, sym.Value, rootVariableCollection);
                        stable &= computed.TryGetValue(symbol.Key, out bool currentValue) && currentValue == value;
                        rootVariableCollection[symbol.Key] = value;
                        computed[symbol.Key] = value;
                    }
                    else if (string.Equals(symbol.Value.Type, SymbolModelConverter.BindSymbolTypeName, StringComparison.Ordinal))
                    {
                        if (parameters.TryGetRuntimeValue(EnvironmentSettings, symbol.Value.Binding, out object bindValue) && bindValue != null)
                        {
                            rootVariableCollection[symbol.Key] = RunnableProjectGenerator.InferTypeAndConvertLiteral(bindValue.ToString());
                        }
                    }
                }
            }

            // evaluate the file glob (specials) conditions
            // the result is needed for SpecialOperationConfig
            foreach (ICustomFileGlobModel fileGlobModel in _specialCustomSetup)
            {
                fileGlobModel.EvaluateCondition(EnvironmentSettings, rootVariableCollection);
            }

            parameters.ResolvedValues.TryGetValue(NameParameter, out object resolvedNameParamValue);

            _sources = EvaluateSources(parameters, rootVariableCollection, configFile, resolvedNameParamValue);

            // evaluate the conditions and resolve the paths for the PrimaryOutputs
            foreach (ICreationPathModel pathModel in PrimaryOutputs)
            {
                pathModel.EvaluateCondition(EnvironmentSettings, rootVariableCollection);

                if (pathModel.ConditionResult)
                {
                    pathModel.PathResolved = FileRenameGenerator.ApplyRenameToPrimaryOutput(
                        pathModel.PathOriginal,
                        EnvironmentSettings,
                        SourceName,
                        resolvedNameParamValue,
                        parameters,
                        SymbolFilenameReplacements);
                }
            }
        }

        private List<FileSourceMatchInfo> EvaluateSources(IParameterSet parameters, IVariableCollection rootVariableCollection, IFileSystemInfo configFile, object resolvedNameParamValue)
        {
            List<FileSourceMatchInfo> sources = new List<FileSourceMatchInfo>();

            foreach (ExtendedFileSource source in Sources)
            {
                if (!string.IsNullOrEmpty(source.Condition) && !Cpp2StyleEvaluatorDefinition.EvaluateFromString(EnvironmentSettings, source.Condition, rootVariableCollection))
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
                        if (string.IsNullOrEmpty(modifier.Condition) || Cpp2StyleEvaluatorDefinition.EvaluateFromString(EnvironmentSettings, modifier.Condition, rootVariableCollection))
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
                                    fileRenamesFromSource[property.Name] = property.Value.Value<string>();
                                }
                            }
                        }
                    }
                }

                string sourceDirectory = source.Source ?? "./";
                string targetDirectory = source.Target ?? "./";
                IReadOnlyDictionary<string, string> allRenamesForSource = AugmentRenames(configFile, sourceDirectory, ref targetDirectory, resolvedNameParamValue, parameters, fileRenamesFromSource);

                FileSourceMatchInfo sourceMatcher = new FileSourceMatchInfo(
                    sourceDirectory,
                    targetDirectory,
                    topLevelPatterns,
                    allRenamesForSource,
                    modifierList);
                sources.Add(sourceMatcher);
            }

            if (Sources.Count == 0)
            {
                IReadOnlyList<string> includePattern = IncludePatternDefaults;
                IReadOnlyList<string> excludePattern = ExcludePatternDefaults;
                IReadOnlyList<string> copyOnlyPattern = CopyOnlyPatternDefaults;
                FileSourceEvaluable topLevelPatterns = new FileSourceEvaluable(includePattern, excludePattern, copyOnlyPattern);

                string targetDirectory = string.Empty;
                Dictionary<string, string> fileRenamesFromSource = new Dictionary<string, string>(StringComparer.Ordinal);
                IReadOnlyDictionary<string, string> allRenamesForSource = AugmentRenames(configFile, "./", ref targetDirectory, resolvedNameParamValue, parameters, fileRenamesFromSource);

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
            return FileRenameGenerator.AugmentFileRenames(EnvironmentSettings, SourceName, configFile, sourceDirectory, ref targetDirectory, resolvedNameParamValue, parameters, fileRenames, SymbolFilenameReplacements);
        }

        private static ISymbolModel SetupDefaultNameSymbol(string sourceName)
        {
            StringBuilder nameSymbolConfigBuilder = new StringBuilder(512);

            nameSymbolConfigBuilder.AppendLine(@"
{
  ""binding"": """ + NameSymbolName + @""",
  ""type"": """ + ParameterSymbol.TypeName + @""",
  ""description"": ""The default name symbol"",
  ""datatype"": ""string"",
  ""forms"": {
    ""global"": [ """ + IdentityValueForm.FormName
                    + @""", """ + DefaultSafeNameValueFormModel.FormName
                    + @""", """ + DefaultLowerSafeNameValueFormModel.FormName
                    + @""", """ + DefaultSafeNamespaceValueFormModel.FormName
                    + @""", """ + DefaultLowerSafeNamespaceValueFormModel.FormName
                    + @"""]
  }
");

            if (!string.IsNullOrEmpty(sourceName))
            {
                nameSymbolConfigBuilder.AppendLine(",");
                nameSymbolConfigBuilder.AppendLine($"\"replaces\": \"{sourceName}\"");
            }

            nameSymbolConfigBuilder.AppendLine("}");

            JObject config = JObject.Parse(nameSymbolConfigBuilder.ToString());
            return ParameterSymbol.FromJObject(config, null, null);
        }

        private static IReadOnlyDictionary<string, IValueForm> SetupValueFormMapForTemplate(JObject source)
        {
            Dictionary<string, IValueForm> formMap = new Dictionary<string, IValueForm>(StringComparer.Ordinal);

            // setup all the built-in default forms.
            foreach (KeyValuePair<string, IValueForm> builtInForm in ValueFormRegistry.AllForms)
            {
                formMap[builtInForm.Key] = builtInForm.Value;
            }

            // setup the forms defined by the template configuration.
            // if any have the same name as a default, the default is overridden.
            IReadOnlyDictionary<string, JToken> templateDefinedforms = source.ToJTokenDictionary(StringComparer.OrdinalIgnoreCase, nameof(Forms));

            foreach (KeyValuePair<string, JToken> form in templateDefinedforms)
            {
                if (form.Value is JObject o)
                {
                    formMap[form.Key] = ValueFormRegistry.GetForm(form.Key, o);
                }
            }

            return formMap;
        }

        internal static SimpleConfigModel FromJObject(IEngineEnvironmentSettings environmentSettings, JObject source, ISimpleConfigModifiers configModifiers = null, JObject localeSource = null)
        {
            ILocalizationModel localizationModel = LocalizationFromJObject(localeSource);

            SimpleConfigModel config = new SimpleConfigModel()
            {
                Author = localizationModel?.Author ?? source.ToString(nameof(config.Author)),
                Classifications = source.ArrayAsStrings(nameof(config.Classifications)),
                DefaultName = source.ToString(nameof(DefaultName)),
                Description = localizationModel?.Description ?? source.ToString(nameof(Description)) ?? string.Empty,
                GroupIdentity = source.ToString(nameof(GroupIdentity)),
                Precedence = source.ToInt32(nameof(Precedence)),
                Guids = source.ArrayAsGuids(nameof(config.Guids)),
                Identity = source.ToString(nameof(config.Identity)),
                Name = localizationModel?.Name ?? source.ToString(nameof(config.Name)),

                SourceName = source.ToString(nameof(config.SourceName)),
                PlaceholderFilename = source.ToString(nameof(config.PlaceholderFilename)),
                EnvironmentSettings = environmentSettings,
                GeneratorVersions = source.ToString(nameof(config.GeneratorVersions))
            };

            JToken shortNameToken = source.Get<JToken>("ShortName");
            config.ShortNameList = JTokenStringOrArrayToCollection(shortNameToken, Array.Empty<string>());

            config.Forms = SetupValueFormMapForTemplate(source);

            List<ExtendedFileSource> sources = new List<ExtendedFileSource>();
            config.Sources = sources;

            foreach (JObject item in source.Items<JObject>(nameof(config.Sources)))
            {
                ExtendedFileSource src = new ExtendedFileSource();
                sources.Add(src);
                src.CopyOnly = item.Get<JToken>(nameof(src.CopyOnly));
                src.Exclude = item.Get<JToken>(nameof(src.Exclude));
                src.Include = item.Get<JToken>(nameof(src.Include));
                src.Condition = item.ToString(nameof(src.Condition));
                src.Rename = item.Get<JObject>(nameof(src.Rename)).ToStringDictionary().ToDictionary(x => x.Key, x => x.Value);

                List<SourceModifier> modifiers = new List<SourceModifier>();
                src.Modifiers = modifiers;
                foreach (JObject entry in item.Items<JObject>(nameof(src.Modifiers)))
                {
                    SourceModifier modifier = new SourceModifier
                    {
                        Condition = entry.ToString(nameof(modifier.Condition)),
                        CopyOnly = entry.Get<JToken>(nameof(modifier.CopyOnly)),
                        Exclude = entry.Get<JToken>(nameof(modifier.Exclude)),
                        Include = entry.Get<JToken>(nameof(modifier.Include)),
                        Rename = entry.Get<JObject>(nameof(modifier.Rename))
                    };
                    modifiers.Add(modifier);
                }

                src.Source = item.ToString(nameof(src.Source));
                src.Target = item.ToString(nameof(src.Target));
            }

            IBaselineInfo baseline = null;
            config.BaselineInfo = BaselineInfoFromJObject(source.PropertiesOf("baselines"));

            if (configModifiers != null && !string.IsNullOrEmpty(configModifiers.BaselineName))
            {
                config.BaselineInfo.TryGetValue(configModifiers.BaselineName, out baseline);
            }

            Dictionary<string, ISymbolModel> symbols = new Dictionary<string, ISymbolModel>(StringComparer.Ordinal);
            // create a name symbol. If one is explicitly defined in the template, it'll override this.
            symbols.Add(NameSymbolName, SetupDefaultNameSymbol(config.SourceName));

            // tags are being deprecated from template configuration, but we still read them for backwards compatibility.
            // They're turned into symbols here, which eventually become tags.
            config._tagsDeprecated = source.ToStringDictionary(StringComparer.OrdinalIgnoreCase, nameof(config.Tags));
            IReadOnlyDictionary<string, ISymbolModel> symbolsFromTags = ConvertDeprecatedTagsToParameterSymbols(config._tagsDeprecated);

            foreach (KeyValuePair<string, ISymbolModel> tagSymbol in symbolsFromTags)
            {
                if (!symbols.ContainsKey(tagSymbol.Key))
                {
                    symbols.Add(tagSymbol.Key, tagSymbol.Value);
                }
            }

            config.Symbols = symbols;
            foreach (JProperty prop in source.PropertiesOf(nameof(config.Symbols)))
            {
                JObject obj = prop.Value as JObject;

                if (obj == null)
                {
                    continue;
                }

                IParameterSymbolLocalizationModel symbolLocalization = null;
                if (localizationModel != null)
                {
                    localizationModel.ParameterSymbols.TryGetValue(prop.Name, out symbolLocalization);
                }

                string defaultOverride = null;
                if (baseline != null && baseline.DefaultOverrides != null)
                {
                    baseline.DefaultOverrides.TryGetValue(prop.Name, out defaultOverride);
                }

                ISymbolModel modelForSymbol = SymbolModelConverter.GetModelForObject(obj, symbolLocalization, defaultOverride);

                if (modelForSymbol != null)
                {
                    // The symbols dictionary comparer is Ordinal, making symbol names case-sensitive.
                    if (string.Equals(prop.Name, NameSymbolName, StringComparison.Ordinal)
                            && symbols.TryGetValue(prop.Name, out ISymbolModel existingSymbol))
                    {
                        // "name" symbol is explicitly defined above. If it's also defined in the template.json, it gets special handling here.
                        symbols[prop.Name] = ParameterSymbol.ExplicitNameSymbolMergeWithDefaults(modelForSymbol, existingSymbol);
                    }
                    else
                    {
                        // last in wins (in the odd case where a template.json defined a symbol multiple times)
                        symbols[prop.Name] = modelForSymbol;
                    }
                }
            }

            config.PostActionModel = RunnableProjects.PostActionModel.ListFromJArray(source.Get<JArray>("PostActions"), localizationModel?.PostActions);
            config.HasScriptRunningPostActions = config.PostActionModel.Any(x => x.ActionId == PostActionInfo.ProcessStartPostActionProcessorId);
            config.PrimaryOutputs = CreationPathModel.ListFromJArray(source.Get<JArray>(nameof(PrimaryOutputs)));

            // Custom operations at the global level
            JToken globalCustomConfigData = source[nameof(config._customOperations)];
            if (globalCustomConfigData != null)
            {
                config._customOperations = CustomFileGlobModel.FromJObject((JObject)globalCustomConfigData, string.Empty);
            }
            else
            {
                config._customOperations = null;
            }

            // Custom operations for specials
            IReadOnlyDictionary<string, JToken> allSpecialOpsConfig = source.ToJTokenDictionary(StringComparer.OrdinalIgnoreCase, "SpecialCustomOperations");
            List<ICustomFileGlobModel> specialCustomSetup = new List<ICustomFileGlobModel>();

            foreach (KeyValuePair<string, JToken> globConfigKeyValue in allSpecialOpsConfig)
            {
                string globName = globConfigKeyValue.Key;
                JToken globData = globConfigKeyValue.Value;

                CustomFileGlobModel globModel = CustomFileGlobModel.FromJObject((JObject)globData, globName);
                specialCustomSetup.Add(globModel);
            }

            config._specialCustomSetup = specialCustomSetup;

            // localization operations for individual files
            Dictionary<string, IReadOnlyList<IOperationProvider>> localizations = new Dictionary<string, IReadOnlyList<IOperationProvider>>();
            if (localizationModel != null && localizationModel.FileLocalizations != null)
            {
                foreach (FileLocalizationModel fileLocalization in localizationModel.FileLocalizations)
                {
                    List<IOperationProvider> localizationsForFile = new List<IOperationProvider>();
                    foreach (KeyValuePair<string, string> localizationInfo in fileLocalization.Localizations)
                    {
                        localizationsForFile.Add(new Replacement(localizationInfo.Key.TokenConfig(), localizationInfo.Value, null, true));
                    }

                    localizations.Add(fileLocalization.File, localizationsForFile);
                }
            }
            config.LocalizationOperations = localizations;

            return config;
        }

        private static IReadOnlyDictionary<string, IBaselineInfo> BaselineInfoFromJObject(IEnumerable<JProperty> baselineJProperties)
        {
            Dictionary<string, IBaselineInfo> allBaselines = new Dictionary<string, IBaselineInfo>();

            foreach (JProperty property in baselineJProperties)
            {
                JObject obj = property.Value as JObject;

                if (obj == null)
                {
                    continue;
                }

                BaselineInfo baseline = new BaselineInfo
                {
                    Description = obj.ToString(nameof(baseline.Description)),
                    DefaultOverrides = obj.Get<JObject>(nameof(baseline.DefaultOverrides)).ToStringDictionary()
                };

                allBaselines[property.Name] = baseline;
            }

            return allBaselines;
        }

        private static IReadOnlyDictionary<string, ISymbolModel> ConvertDeprecatedTagsToParameterSymbols(IReadOnlyDictionary<string, string> tagsDeprecated)
        {
            Dictionary<string, ISymbolModel> symbols = new Dictionary<string, ISymbolModel>();

            foreach (KeyValuePair<string, string> tagInfo in tagsDeprecated)
            {
                symbols[tagInfo.Key] = ParameterSymbol.FromDeprecatedConfigTag(tagInfo.Value);
            }

            return symbols;
        }

        internal static ILocalizationModel LocalizationFromJObject(JObject source)
        {
            if (source == null)
            {
                return null;
            }

            LocalizationModel locModel = new LocalizationModel()
            {
                Author = source.ToString(nameof(locModel.Author)),
                Name = source.ToString(nameof(locModel.Name)),
                Description = source.ToString(nameof(locModel.Description)),
                Identity = source.ToString(nameof(locModel.Identity)),
            };

            // symbol description & choice localizations
            Dictionary<string, IParameterSymbolLocalizationModel> parameterLocalizations = new Dictionary<string, IParameterSymbolLocalizationModel>();
            IReadOnlyDictionary<string, JToken> symbolInfoList = source.ToJTokenDictionary(StringComparer.OrdinalIgnoreCase, "symbols");

            foreach (KeyValuePair<string, JToken> symbolDetail in symbolInfoList)
            {
                string symbol = symbolDetail.Key;
                string displayName = symbolDetail.Value.ToString("displayName");
                string description = symbolDetail.Value.ToString("description");
                var choiceLocalizations = new Dictionary<string, ParameterChoiceLocalizationModel>();

                foreach (JObject choiceObject in symbolDetail.Value.Items<JObject>("choices"))
                {
                    string choiceName = choiceObject.ToString("choice");
                    if (string.IsNullOrEmpty(choiceName))
                    {
                        continue;
                    }

                    choiceLocalizations.Add(choiceName, new ParameterChoiceLocalizationModel(
                        choiceObject.ToString("displayName"),
                        choiceObject.ToString("description")));
                }

                var symbolLocalization = new ParameterSymbolLocalizationModel(
                    symbol,
                    displayName,
                    description,
                    choiceLocalizations);
                parameterLocalizations.Add(symbol, symbolLocalization);
            }

            locModel.ParameterSymbols = parameterLocalizations;

            // post action localizations
            Dictionary<Guid, IPostActionLocalizationModel> postActions = new Dictionary<Guid, IPostActionLocalizationModel>();
            foreach (JObject item in source.Items<JObject>(nameof(locModel.PostActions)))
            {
                IPostActionLocalizationModel postActionModel = PostActionLocalizationModel.FromJObject(item);
                postActions.Add(postActionModel.ActionId, postActionModel);
            }
            locModel.PostActions = postActions;

            // regular file localizations
            IReadOnlyDictionary<string, JToken> fileLocalizationJson = source.ToJTokenDictionary(StringComparer.OrdinalIgnoreCase, "localizations");
            List<FileLocalizationModel> fileLocalizations = new List<FileLocalizationModel>();

            if (fileLocalizationJson != null)
            {
                foreach (KeyValuePair<string, JToken> fileLocInfo in fileLocalizationJson)
                {
                    string fileName = fileLocInfo.Key;
                    JToken localizationJson = fileLocInfo.Value;
                    FileLocalizationModel fileModel = FileLocalizationModel.FromJObject(fileName, (JObject)localizationJson);
                    fileLocalizations.Add(fileModel);
                }
            }
            locModel.FileLocalizations = fileLocalizations;

            return locModel;
        }
    }
}
