using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class SimpleConfigModel : IRunnableProjectConfig
    {
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

        private static readonly IReadOnlyDictionary<string, string> RenameDefaults = new Dictionary<string, string>();

        public SimpleConfigModel()
        {
            Sources = new[] { new ExtendedFileSource() };
        }

        private IReadOnlyDictionary<string, Parameter> _parameters;
        private IReadOnlyList<FileSource> _sources;
        private IGlobalRunConfig _operationConfig;
        private IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> _specialOperationConfig;
        private Parameter _nameParameter;
        private string _safeNameName;
        private string _safeNamespaceName;
        private string _lowerSafeNameName;
        private string _lowerSafeNamespaceName;

        public IFile SourceFile { get; set; }

        public string Author { get; set; }

        public IReadOnlyList<string> Classifications { get; set; }

        public string DefaultName { get; set; }

        public string Description { get; set; }

        public string GroupIdentity { get; set; }

        public IReadOnlyList<Guid> Guids { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public string SourceName { get; set; }

        public IReadOnlyList<ExtendedFileSource> Sources { get; set; }

        public IReadOnlyDictionary<string, ISymbolModel> Symbols { get; set; }

        public IReadOnlyList<IPostActionModel> PostActionModel { get; set; }

        public IReadOnlyList<ICreationPathModel> PrimaryOutputs { get; set; }

        public IReadOnlyDictionary<string, string> Tags { get; set; }

        IReadOnlyDictionary<string, string> IRunnableProjectConfig.Tags => Tags;

        IReadOnlyList<string> IRunnableProjectConfig.Classifications => Classifications;

        public string Identity { get; set; }

        private static readonly string DefaultPlaceholderFilename = "-.-";

        private string _placeholderValue;

        public string PlaceholderFilename
        {
            get
            {
                return _placeholderValue;
            }
            set
            {
                _placeholderValue = value ?? DefaultPlaceholderFilename;
            }
        }

        IReadOnlyDictionary<string, Parameter> IRunnableProjectConfig.Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter>();

                    if (Symbols == null)
                    {
                        parameters["name"] = new Parameter
                        {
                            IsName = true,
                            Requirement = TemplateParameterPriority.Implicit,
                            Name = "name",
                            DefaultValue = DefaultName ?? SourceName
                        };
                    }
                    else
                    {
                        foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                        {
                            if (symbol.Value.Type == "parameter")
                            {
                                ParameterSymbol param = (ParameterSymbol)symbol.Value;
                                bool isName = param.Binding == "name";
                                parameters[symbol.Key] = new Parameter
                                {
                                    DefaultValue = param.DefaultValue ?? (!param.IsRequired ? param.Replaces : null),
                                    Description = param.Description,
                                    IsName = isName,
                                    IsVariable = true,
                                    Name = symbol.Key,
                                    Requirement = param.IsRequired ? TemplateParameterPriority.Required : isName ? TemplateParameterPriority.Implicit : TemplateParameterPriority.Optional,
                                    Type = param.Type,
                                    DataType = param.DataType,
                                    Choices = param.Choices
                                };
                            }
                        }
                    }

                    // TODO: move this into the above else. it only makes sense in that context
                    string nameParameter = parameters.FirstOrDefault(x => x.Value.IsName).Key;

                    if (nameParameter == null)
                    {
                        parameters["name"] = new Parameter
                        {
                            IsName = true,
                            Requirement = TemplateParameterPriority.Implicit,
                            Name = "name",
                            DefaultValue = DefaultName ?? SourceName,
                            IsVariable = true
                        };
                    }

                    _parameters = parameters;
                }

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

        IReadOnlyList<FileSource> IRunnableProjectConfig.Sources
        {
            get
            {
                if (_sources == null)
                {
                    List<FileSource> sources = new List<FileSource>();

                    foreach (ExtendedFileSource source in Sources)
                    {
                        IReadOnlyList<string> includePattern = JTokenToCollection(source.Include, SourceFile, IncludePatternDefaults);
                        IReadOnlyList<string> excludePattern = JTokenToCollection(source.Exclude, SourceFile, ExcludePatternDefaults);
                        IReadOnlyList<string> copyOnlyPattern = JTokenToCollection(source.CopyOnly, SourceFile, CopyOnlyPatternDefaults);
                        IReadOnlyDictionary<string, string> renamePatterns = source.Rename ?? RenameDefaults;

                        sources.Add(new FileSource
                        {
                            CopyOnly = copyOnlyPattern,
                            Exclude = excludePattern,
                            Include = includePattern,
                            Source = source.Source ?? "./",
                            Target = source.Target ?? "./",
                            Rename = renamePatterns
                        });
                    }

                    if (sources.Count == 0)
                    {
                        IReadOnlyList<string> includePattern = IncludePatternDefaults;
                        IReadOnlyList<string> excludePattern = ExcludePatternDefaults;
                        IReadOnlyList<string> copyOnlyPattern = CopyOnlyPatternDefaults;

                        sources.Add(new FileSource
                        {
                            CopyOnly = copyOnlyPattern,
                            Exclude = excludePattern,
                            Include = includePattern,
                            Source = "./",
                            Target = "./",
                            Rename = null
                        });
                    }

                    _sources = sources;
                }

                return _sources;
            }
        }

        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new Dictionary<Guid, string>();

        // operation info read from the config
        private ICustomFileGlobModel CustomOperations = new CustomFileGlobModel();

        IGlobalRunConfig IRunnableProjectConfig.OperationConfig
        {
            get
            {
                if (_operationConfig == null)
                {
                    SpecialOperationConfigParams defaultOperationParams = new SpecialOperationConfigParams(string.Empty, "//", "C++", ConditionalType.CLineComments);
                    _operationConfig = ProduceOperationSetup(defaultOperationParams, true, CustomOperations);
                }

                return _operationConfig;
            }
        }

        private class SpecialOperationConfigParams
        {
            public SpecialOperationConfigParams(string glob, string flagPrefix, string evaluatorName, ConditionalType type)
            {
                EvaluatorName = evaluatorName;
                Glob = glob;
                FlagPrefix = flagPrefix;
                ConditionalStyle = type;
            }

            public string Glob { get; }

            public string EvaluatorName { get; }

            public string FlagPrefix { get; }

            public ConditionalType ConditionalStyle { get; }

            public string VariableFormat { get; set; }

            private static readonly SpecialOperationConfigParams _Defaults = new SpecialOperationConfigParams(string.Empty, string.Empty, "C++", ConditionalType.None);

            public static SpecialOperationConfigParams Defaults
            {
                get
                {
                    return _Defaults;
                }
            }

        }

        // file -> replacements
        public IReadOnlyDictionary<string, IReadOnlyList<IOperationProvider>> LocalizationOperations { get; private set; }


        private IReadOnlyList<ICustomFileGlobModel> SpecialCustomSetup = new List<ICustomFileGlobModel>();

        IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> IRunnableProjectConfig.SpecialOperationConfig
        {
            get
            {
                if (_specialOperationConfig == null)
                {
                    List<SpecialOperationConfigParams> defaultSpecials = new List<SpecialOperationConfigParams>
                    {
                        new SpecialOperationConfigParams("**/*.json", "//", "C++", ConditionalType.CLineComments),
                        new SpecialOperationConfigParams("**/*.css.min", "/*", "C++", ConditionalType.CBlockComments),
                        new SpecialOperationConfigParams("**/*.css", "/*", "C++", ConditionalType.CBlockComments),
                        new SpecialOperationConfigParams("**/*.cshtml", "@*", "C++", ConditionalType.Razor),
                        new SpecialOperationConfigParams("**/*.cs", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.fs", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.cpp", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.h", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.hpp", "//", "C++", ConditionalType.CNoComments),
                        new SpecialOperationConfigParams("**/*.*proj", "<!--/", "MSBUILD", ConditionalType.MSBuild),
                        new SpecialOperationConfigParams("**/*.*htm", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.*html", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.jsp", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.asp", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.aspx", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.bat", "rem --:", "C++", ConditionalType.RemLineComment),
                        new SpecialOperationConfigParams("**/*.cmd", "rem --:", "C++", ConditionalType.RemLineComment),
                        new SpecialOperationConfigParams("**/nginx.conf", "#--", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/robots.txt", "#--", "C++", ConditionalType.HashSignLineComment),
                        new SpecialOperationConfigParams("**/*.haml", "-#", "C++", ConditionalType.HamlLineComment),
                        new SpecialOperationConfigParams("**/*.jsx", "{/*", "C++", ConditionalType.JsxBlockComment),
                        new SpecialOperationConfigParams("**/*.tsx", "{/*", "C++", ConditionalType.JsxBlockComment),
                        new SpecialOperationConfigParams("**/*.xml", "<!--", "C++", ConditionalType.Xml),
                        new SpecialOperationConfigParams("**/*.xaml", "<!--", "C++", ConditionalType.Xml)
                    };
                    List<KeyValuePair<string, IGlobalRunConfig>> specialOperationConfig = new List<KeyValuePair<string, IGlobalRunConfig>>();

                    // put the custom configs first in the list
                    HashSet<string> processedGlobs = new HashSet<string>();

                    foreach (ICustomFileGlobModel customGlobModel in SpecialCustomSetup)
                    {
                        if (customGlobModel.ConditionResult)
                        {   // only add the special if the condition is true
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
                        {   // this one was already setup due to a custom config
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

        public IEngineEnvironmentSettings EnvironmentSettings { get; private set; }

        private IGlobalRunConfig ProduceOperationSetup(SpecialOperationConfigParams defaultModel, bool generateMacros, ICustomFileGlobModel customGlobModel = null)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();

            // TODO: if we allow custom config to specify a built-in conditional type, decide what to do.
            if (defaultModel.ConditionalStyle != ConditionalType.None)
            {
                operations.AddRange(ConditionalConfig.ConditionalSetup(defaultModel.ConditionalStyle, defaultModel.EvaluatorName, true, true, null));
            }

            if (customGlobModel == null || string.IsNullOrEmpty(customGlobModel.FlagPrefix))
            {   // these conditions may need to be separated - if there is custom info, but the flag prefix was not provided, we might want to raise a warning / error
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

            IReadOnlyList<IMacroConfig> macros = null;
            List<IMacroConfig> computedMacros = new List<IMacroConfig>();
            List<IReplacementTokens> macroGeneratedReplacements = new List<IReplacementTokens>();

            if (generateMacros)
            {
                macros = ProduceMacroConfig(computedMacros);
            }

            if (SourceName != null)
            {
                if (SourceName.ToLower() != SourceName)
                {
                    if (SourceName.IndexOf('.') > -1)
                    {
                        macroGeneratedReplacements.Add(new ReplacementTokens(_lowerSafeNamespaceName, SourceName.ToLowerInvariant()));
                        macroGeneratedReplacements.Add(new ReplacementTokens(_lowerSafeNameName, SourceName.ToLowerInvariant().Replace('.', '_')));
                    }
                    else
                    {
                        macroGeneratedReplacements.Add(new ReplacementTokens(_lowerSafeNameName, SourceName.ToLowerInvariant()));
                    }
                }

                if (SourceName.IndexOf('.') > -1)
                {
                    macroGeneratedReplacements.Add(new ReplacementTokens(_safeNamespaceName, SourceName));
                    macroGeneratedReplacements.Add(new ReplacementTokens(_safeNameName, SourceName.Replace('.', '_')));
                }
                else
                {
                    macroGeneratedReplacements.Add(new ReplacementTokens(_safeNameName, SourceName));
                }
            }

            if (Symbols != null)
            {
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (symbol.Value.Replaces != null)
                    {
                        if (symbol.Value.Type == "bind")
                        {
                            if (string.IsNullOrWhiteSpace(symbol.Value.Binding))
                            {
                                EnvironmentSettings.Host.LogMessage($"Binding wasn't specified for bind-type symbol {symbol.Key}");
                            }
                            else
                            {
                                //Since this is a bind symbol, don't replace the literal with this symbol's value, 
                                //  replace it with the value of the bound symbol
                                macroGeneratedReplacements.Add(new ReplacementTokens(symbol.Value.Binding, symbol.Value.Replaces));
                            }
                        }
                        else
                        {
                            //Replace the literal value in the "replaces" property with the evaluated value of the symbol
                            macroGeneratedReplacements.Add(new ReplacementTokens(symbol.Key, symbol.Value.Replaces));
                        }
                    }
                }
            }

            foreach (KeyValuePair<Guid, string> map in _guidToGuidPrefixMap)
            {
                foreach (char format in GuidMacroConfig.DefaultFormats)
                {
                    string newGuid = char.IsUpper(format) ? map.Key.ToString(format.ToString()).ToUpperInvariant() : map.Key.ToString(format.ToString()).ToLowerInvariant();
                    macroGeneratedReplacements.Add(new ReplacementTokens(map.Value + "-" + format, newGuid));
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
            
            GlobalRunConfig config = new GlobalRunConfig()
            {
                Operations = operations,
                VariableSetup = variableConfig,
                Macros = macros,
                ComputedMacros = computedMacros,
                Replacements = macroGeneratedReplacements,
                CustomOperations = customOperationConfig,
            };

            return config;
        }

        private IReadOnlyList<IMacroConfig> ProduceMacroConfig(List<IMacroConfig> computedMacroConfigs)
        {
            List<IMacroConfig> generatedMacroConfigs = new List<IMacroConfig>();

            if (Guids != null)
            {
                int guidCount = 0;
                foreach (Guid guid in Guids)
                {
                    int id = guidCount++;
                    string replacementId = "guid" + id;
                    generatedMacroConfigs.Add(new GuidMacroConfig(replacementId, null));
                    _guidToGuidPrefixMap[guid] = replacementId;
                }
            }

            if (Symbols != null)
            {
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (symbol.Value.Binding == "safe_name")
                    {
                        _safeNameName = symbol.Key; // save for later, to generate the safe_name regex macro
                    }
                    else if (symbol.Value.Binding == "lower_safe_name")
                    {
                        _lowerSafeNameName = symbol.Key; // save for later, to generate the safe_name regex macro
                    }
                    else if (symbol.Value.Binding == "safe_namespace_name")
                    {
                        _safeNamespaceName = symbol.Key; // save for later, to generate the safe_name regex macro
                    }
                    else if (symbol.Value.Binding == "lower_safe_namespace_name")
                    {
                        _lowerSafeNamespaceName = symbol.Key; // save for later, to generate the safe_name regex macro
                    }

                    if (symbol.Value.Type == "computed")
                    {
                        string value = ((ComputedSymbol)symbol.Value).Value;
                        string evaluator = ((ComputedSymbol)symbol.Value).Evaluator;
                        computedMacroConfigs.Add(new EvaluateMacroConfig(symbol.Key, value, evaluator));
                    }
                    else if (symbol.Value.Type == "generated")
                    {
                        GeneratedSymbol symbolInfo = (GeneratedSymbol)symbol.Value;
                        string type = symbolInfo.Generator;
                        string variableName = symbol.Key;
                        Dictionary<string, JToken> configParams = new Dictionary<string, JToken>();

                        foreach (KeyValuePair<string, JToken> parameter in symbolInfo.Parameters)
                        {
                            configParams.Add(parameter.Key, parameter.Value);
                        }

                        generatedMacroConfigs.Add(new GeneratedSymbolDeferredMacroConfig(type, variableName, configParams));
                    }
                }
            }

            if (_safeNameName == null)
            {
                IList<KeyValuePair<string, string>> steps = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(@"(^\s+|\s+$)", ""),
                    new KeyValuePair<string, string>(@"(((?<=\.)|^)(?=\d)|\W)", "_")
                };

                generatedMacroConfigs.Add(new RegexMacroConfig("safe_name", NameParameter.Name, steps));
                _safeNameName = "safe_name";
            }

            if (_lowerSafeNameName == null)
            {
                generatedMacroConfigs.Add(new CaseChangeMacroConfig("lower_safe_name", _safeNameName, true));
                _lowerSafeNameName = "lower_safe_name";
            }

            if (_safeNamespaceName == null)
            {
                IList<KeyValuePair<string, string>> steps = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(@"(^\s+|\s+$)", ""),
                    new KeyValuePair<string, string>(@"(((?<=\.)|^)(?=\d)|[^\w\.])", "_")
                };

                generatedMacroConfigs.Add(new RegexMacroConfig("safe_namespace", NameParameter.Name, steps));
                _safeNamespaceName = "safe_namespace";
            }

            if (_lowerSafeNamespaceName == null)
            {
                generatedMacroConfigs.Add(new CaseChangeMacroConfig("lower_safe_namespace", _safeNamespaceName, true));
                _lowerSafeNamespaceName = "lower_safe_namespace";
            }

            return generatedMacroConfigs;
        }

        private static IReadOnlyList<string> JTokenToCollection(JToken token, IFile sourceFile, string[] defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.Type == JTokenType.String)
            {
                using (Stream excludeList = sourceFile.Parent.Parent.FileInfo(token.ToString()).OpenRead())
                using (TextReader reader = new StreamReader(excludeList, Encoding.UTF8, true, 4096, true))
                {
                    return reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }

            return token.ArrayAsStrings();
        }

        public void Evaluate(IParameterSet parameters, IVariableCollection rootVariableCollection, IFileSystemInfo configFile)
        {
            List<FileSource> sources = new List<FileSource>();
            bool stable = Symbols == null;
            Dictionary<string, bool> computed = new Dictionary<string, bool>();

            while (!stable)
            {
                stable = true;
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (symbol.Value.Type == "computed")
                    {
                        ComputedSymbol sym = (ComputedSymbol)symbol.Value;
                        bool value = CppStyleEvaluatorDefinition.EvaluateFromString(EnvironmentSettings, sym.Value, rootVariableCollection);
                        stable &= computed.TryGetValue(symbol.Key, out bool currentValue) && currentValue == value;
                        rootVariableCollection[symbol.Key] = value;
                        computed[symbol.Key] = value;
                    }
                    else if (symbol.Value.Type == "bind")
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
            foreach (ICustomFileGlobModel fileGlobModel in SpecialCustomSetup)
            {
                fileGlobModel.EvaluateCondition(EnvironmentSettings, rootVariableCollection);
            }

            parameters.ResolvedValues.TryGetValue(NameParameter, out object resolvedNameParamValue);

            // evaluate the conditions and resolve the paths for the PrimaryOutputs
            foreach (ICreationPathModel pathModel in PrimaryOutputs)
            {
                pathModel.EvaluateCondition(EnvironmentSettings, rootVariableCollection);

                if (pathModel.ConditionResult && resolvedNameParamValue != null)
                {
                    if (SourceName != null)
                    {
                        // this path will be included in the outputs, replace the name (same thing we do to other file paths)
                        pathModel.PathResolved = pathModel.PathOriginal.Replace(SourceName, (string)resolvedNameParamValue);
                    }
                    else
                    {
                        pathModel.PathResolved = pathModel.PathOriginal;
                    }
                }
            }

            foreach (ExtendedFileSource source in Sources)
            {
                if (!string.IsNullOrEmpty(source.Condition) && !CppStyleEvaluatorDefinition.EvaluateFromString(EnvironmentSettings, source.Condition, rootVariableCollection))
                {
                    continue;
                }

                List<string> includePattern = JTokenToCollection(source.Include, SourceFile, IncludePatternDefaults).ToList();
                List<string> excludePattern = JTokenToCollection(source.Exclude, SourceFile, ExcludePatternDefaults).ToList();
                List<string> copyOnlyPattern = JTokenToCollection(source.CopyOnly, SourceFile, CopyOnlyPatternDefaults).ToList();

                if (source.Modifiers != null)
                {
                    foreach (SourceModifier modifier in source.Modifiers)
                    {
                        if (string.IsNullOrEmpty(modifier.Condition) || CppStyleEvaluatorDefinition.EvaluateFromString(EnvironmentSettings, modifier.Condition, rootVariableCollection))
                        {
                            includePattern.AddRange(JTokenToCollection(modifier.Include, SourceFile, new string[0]));
                            excludePattern.AddRange(JTokenToCollection(modifier.Exclude, SourceFile, new string[0]));
                            copyOnlyPattern.AddRange(JTokenToCollection(modifier.CopyOnly, SourceFile, new string[0]));
                        }
                    }
                }

                Dictionary<string, string> renames = new Dictionary<string, string>(source.Rename);

                if (resolvedNameParamValue != null && SourceName != null)
                {
                    string targetName = ((string)resolvedNameParamValue).Trim();

                    foreach (IFileSystemInfo entry in configFile.Parent.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                    {
                        string tmpltRel = entry.PathRelativeTo(configFile.Parent.Parent);
                        string outRel = tmpltRel.Replace(SourceName, targetName);
                        renames[tmpltRel] = outRel;
                    }
                }

                sources.Add(new FileSource
                {
                    CopyOnly = copyOnlyPattern.ToArray(),
                    Exclude = excludePattern.ToArray(),
                    Include = includePattern.ToArray(),
                    Source = source.Source ?? "./",
                    Target = source.Target ?? "./",
                    Rename = renames
                });
            }

            if (Sources.Count == 0)
            {
                IReadOnlyList<string> includePattern = IncludePatternDefaults;
                IReadOnlyList<string> excludePattern = ExcludePatternDefaults;
                IReadOnlyList<string> copyOnlyPattern = CopyOnlyPatternDefaults;

                Dictionary<string, string> renames = new Dictionary<string, string>();

                if (SourceName != null)
                {
                    if (parameters.ResolvedValues.TryGetValue(NameParameter, out object resolvedValue))
                    {
                        foreach (IFileSystemInfo entry in configFile.Parent.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                        {
                            string tmpltRel = entry.PathRelativeTo(configFile.Parent.Parent);
                            string outRel = tmpltRel.Replace(SourceName, (string)resolvedValue);
                            renames[tmpltRel] = outRel;
                        }
                    }
                }

                sources.Add(new FileSource
                {
                    CopyOnly = copyOnlyPattern,
                    Exclude = excludePattern,
                    Include = includePattern,
                    Source = "./",
                    Target = "./",
                    Rename = renames
                });
            }

            _sources = sources;
        }

        public static SimpleConfigModel FromJObject(IEngineEnvironmentSettings environmentSettings, JObject source, JObject localeSource = null)
        {
            ILocalizationModel localizationModel = LocalizationFromJObject(localeSource);

            SimpleConfigModel config = new SimpleConfigModel()
            {
                Author = localizationModel?.Author ?? source.ToString(nameof(config.Author)),
                Classifications = source.ArrayAsStrings(nameof(config.Classifications)),
                DefaultName = source.ToString(nameof(DefaultName)),
                Description = localizationModel?.Description ?? source.ToString(nameof(Description)),
                GroupIdentity = source.ToString(nameof(GroupIdentity)),
                Guids = source.ArrayAsGuids(nameof(config.Guids)),
                Identity = source.ToString(nameof(config.Identity)),
                Name = localizationModel?.Name ?? source.ToString(nameof(config.Name)),
                ShortName = source.ToString(nameof(config.ShortName)),
                SourceName = source.ToString(nameof(config.SourceName)),
                PlaceholderFilename = source.ToString(nameof(config.PlaceholderFilename)),
                EnvironmentSettings = environmentSettings
            };

            List <ExtendedFileSource> sources = new List<ExtendedFileSource>();
            config.Sources = sources;

            foreach (JObject item in source.Items<JObject>(nameof(config.Sources)))
            {
                ExtendedFileSource src = new ExtendedFileSource();
                sources.Add(src);
                src.CopyOnly = item.Get<JToken>(nameof(src.CopyOnly));
                src.Exclude = item.Get<JToken>(nameof(src.Exclude));
                src.Include = item.Get<JToken>(nameof(src.Include));
                src.Condition = item.ToString(nameof(src.Condition));

                List<SourceModifier> modifiers = new List<SourceModifier>();
                src.Modifiers = modifiers;
                foreach (JObject entry in item.Items<JObject>(nameof(src.Modifiers)))
                {
                    SourceModifier modifier = new SourceModifier()
                    {
                        Condition = entry.ToString(nameof(modifier.Condition)),
                        CopyOnly = entry.Get<JToken>(nameof(modifier.CopyOnly)),
                        Exclude = entry.Get<JToken>(nameof(modifier.Exclude)),
                        Include = entry.Get<JToken>(nameof(modifier.Include))
                    };
                    modifiers.Add(modifier);
                }

                src.Source = item.ToString(nameof(src.Source));
                src.Target = item.ToString(nameof(src.Target));
            }

            Dictionary<string, ISymbolModel> symbols = new Dictionary<string, ISymbolModel>(StringComparer.Ordinal);
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

                ISymbolModel model = SymbolModelConverter.GetModelForObject(obj, symbolLocalization);

                if (model != null)
                {
                    symbols[prop.Name] = model;
                }
            }

            config.Tags = source.ToStringDictionary(StringComparer.OrdinalIgnoreCase, nameof(config.Tags));
            config.PostActionModel = RunnableProjects.PostActionModel.ListFromJArray((JArray)source["PostActions"], localizationModel?.PostActions);

            config.PrimaryOutputs = CreationPathModel.ListFromJArray((JArray)source["PrimaryOutputs"]);

            // Custom operations at the global level
            JToken globalCustomConfigData = source[nameof(config.CustomOperations)];
            if (globalCustomConfigData != null)
            {
                config.CustomOperations = CustomFileGlobModel.FromJObject((JObject)globalCustomConfigData, string.Empty);
            }
            else
            {
                config.CustomOperations = null;
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

            config.SpecialCustomSetup = specialCustomSetup;

            // localization operations for individual files
            Dictionary<string, IReadOnlyList<IOperationProvider>> localizations = new Dictionary<string, IReadOnlyList<IOperationProvider>>();
            if (localizationModel != null && localizationModel.FileLocalizations != null)
            {
                foreach (FileLocalizationModel fileLocalization in localizationModel.FileLocalizations)
                {
                    List<IOperationProvider> localizationsForFile = new List<IOperationProvider>();
                    foreach (KeyValuePair<string, string> localizationInfo in fileLocalization.Localizations)
                    {
                        localizationsForFile.Add(new Replacement(localizationInfo.Key, localizationInfo.Value, null));
                    }

                    localizations.Add(fileLocalization.File, localizationsForFile);
                }
            }
            config.LocalizationOperations = localizations;

            return config;
        }

        public static ILocalizationModel LocalizationFromJObject(JObject source)
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
                string description = symbolDetail.Value.ToString("description");
                Dictionary<string, string> choicesAndDescriptionsForSymbol = new Dictionary<string, string>();

                foreach (JObject choiceObject in symbolDetail.Value.Items<JObject>("choices"))
                {
                    string choice = choiceObject.ToString("choice");
                    string choiceDescription = choiceObject.ToString("description");

                    if (!string.IsNullOrEmpty(choice) && !string.IsNullOrEmpty(choiceDescription))
                    {
                        choicesAndDescriptionsForSymbol.Add(choice, choiceDescription);
                    }
                }

                IParameterSymbolLocalizationModel symbolLocalization = new ParameterSymbolLocalizationModel(symbol, description, choicesAndDescriptionsForSymbol);
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
