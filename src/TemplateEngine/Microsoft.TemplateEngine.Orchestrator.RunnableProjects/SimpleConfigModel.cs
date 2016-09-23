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
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class SimpleConfigModel : IRunnableProjectConfig
    {
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

        public IFile SourceFile { get; set; }

        public string Author { get; set; }

        public IReadOnlyList<string> Classifications { get; set; }

        public string DefaultName { get; set; }

        public string GroupIdentity { get; set; }

        public IReadOnlyList<Guid> Guids { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public string SourceName { get; set; }

        public IReadOnlyList<ExtendedFileSource> Sources { get; set; }

        public IReadOnlyDictionary<string, ISymbolModel> Symbols { get; set; }

        public IReadOnlyList<IPostActionModel> PostActionModel { get; set; }

        public IReadOnlyDictionary<string, string> Tags { get; set; }

        IReadOnlyDictionary<string, string> IRunnableProjectConfig.Tags => Tags;

        IReadOnlyList<string> IRunnableProjectConfig.Classifications => Classifications;

        public string Identity { get; set; }

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
                        IReadOnlyList<string> includePattern = JTokenToCollection(source.Include, SourceFile, new[] { "**/*" });
                        IReadOnlyList<string> excludePattern = JTokenToCollection(source.Exclude, SourceFile, new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" });
                        IReadOnlyList<string> copyOnlyPattern = JTokenToCollection(source.CopyOnly, SourceFile, new[] { "**/node_modules/**/*" });

                        sources.Add(new FileSource
                        {
                            CopyOnly = copyOnlyPattern,
                            Exclude = excludePattern,
                            Include = includePattern,
                            Source = source.Source ?? "./",
                            Target = source.Target ?? "./",
                            Rename = null
                        });
                    }

                    if (sources.Count == 0)
                    {
                        IReadOnlyList<string> includePattern = new[] { "**/*" };
                        IReadOnlyList<string> excludePattern = new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" };
                        IReadOnlyList<string> copyOnlyPattern = new[] { "**/node_modules/**/*" };

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

        //private static readonly SpecialOperationConfigParams DefaultOperationParams = new SpecialOperationConfigParams(string.Empty, "//", ConditionalType.CLineComments);

        // operation info read from the config
        private ICustomFileGlobModel CustomOperations = new CustomFileGlobModel();

        //IGlobalRunConfig IRunnableProjectConfig.OperationConfig => ProduceOperationSetup(DefaultOperationParams, true, CustomOperations);

        IGlobalRunConfig IRunnableProjectConfig.OperationConfig
        {
            get
            {
                if (_operationConfig == null)
                {
                    SpecialOperationConfigParams defaultOperationParams = new SpecialOperationConfigParams(string.Empty, "//", ConditionalType.CLineComments);
                    _operationConfig = ProduceOperationSetup(defaultOperationParams, true, CustomOperations);
                }

                return _operationConfig;
            }
        }

        private class SpecialOperationConfigParams
        {
            public SpecialOperationConfigParams(string glob, string flagPrefix, ConditionalType type)
            {
                Glob = glob;
                FlagPrefix = flagPrefix;
                ConditionalStyle = type;
            }

            public string Glob { get; }

            public string FlagPrefix { get; }

            public ConditionalType ConditionalStyle { get; }

            private static readonly SpecialOperationConfigParams _Defaults = new SpecialOperationConfigParams(string.Empty, string.Empty, ConditionalType.None);

            public static SpecialOperationConfigParams Defaults
            {
                get
                {
                    return _Defaults;
                }
            }
        }

        private IReadOnlyList<ICustomFileGlobModel> SpecialCustomSetup = new List<ICustomFileGlobModel>();

        IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> IRunnableProjectConfig.SpecialOperationConfig
        {
            get
            {
                if (_specialOperationConfig == null)
                {
                    List<SpecialOperationConfigParams> defaultSpecials = new List<SpecialOperationConfigParams>();
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.json", "//", ConditionalType.CLineComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.css.min", "/*", ConditionalType.CBlockComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.css", "/*", ConditionalType.CBlockComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.cs", "//", ConditionalType.CNoComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.cpp", "//", ConditionalType.CNoComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.h", "//", ConditionalType.CNoComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.hpp", "//", ConditionalType.CNoComments));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.*proj", "<!--/", ConditionalType.Xml));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.*htm", "<!--", ConditionalType.Xml));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.*html", "<!--", ConditionalType.Xml));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.jsp", "<!--", ConditionalType.Xml));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.asp", "<!--", ConditionalType.Xml));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.aspx", "<!--", ConditionalType.Xml));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.bat", "rem --:", ConditionalType.RemLineComment));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/*.cmd", "rem --:", ConditionalType.RemLineComment));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/nginx.conf", "#--", ConditionalType.HashSignLineComment));
                    defaultSpecials.Add(new SpecialOperationConfigParams("**/robots.txt", "#--", ConditionalType.HashSignLineComment));

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

        private IGlobalRunConfig ProduceOperationSetup(SpecialOperationConfigParams defaultModel, bool generateMacros, ICustomFileGlobModel customGlobModel = null)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();

            // TODO: if we allow custom config to specify a built-in conditional type, decide what to do.
            if (defaultModel.ConditionalStyle != ConditionalType.None)
            {
                operations.AddRange(ConditionalConfig.ConditionalSetup(defaultModel.ConditionalStyle, "C++", true, true, null));
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
                variableConfig = VariableConfig.DefaultVariableSetup();
            }

            IReadOnlyList<IMacroConfig> macros = null;
            List<IMacroConfig> computedMacros = new List<IMacroConfig>();
            List<IReplacementTokens> macroGeneratedReplacements = new List<IReplacementTokens>();

            if (generateMacros)
            {
                macros = ProduceMacroConfig(computedMacros);
            }

            macroGeneratedReplacements.Add(new ReplacementTokens(_safeNameName, SourceName));

            if (Symbols != null)
            {
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (symbol.Value.Replaces != null)
                    {
                        macroGeneratedReplacements.Add(new ReplacementTokens(symbol.Value.Replaces, symbol.Key));
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
                CustomOperations = customOperationConfig
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
                    generatedMacroConfigs.Add(new GuidMacroConfig(replacementId, "new", null));
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

                    if (symbol.Value.Type == "computed")
                    {
                        string action = ((ComputedSymbol)symbol.Value).Value;
                        computedMacroConfigs.Add(new EvaluateMacroConfig(symbol.Key, action, EvaluateMacro.DefaultEvaluator));
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
                IList<KeyValuePair<string, string>> steps = new List<KeyValuePair<string, string>>();
                steps.Add(new KeyValuePair<string, string>(@"\W", "_"));
                generatedMacroConfigs.Add(new RegexMacroConfig("safe_name", "replace", NameParameter.Name, steps));
                _safeNameName = "safe_name";
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
                using (Stream excludeList = sourceFile.Parent.FileInfo(token.ToString()).OpenRead())
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
                        bool value = CppStyleEvaluatorDefinition.EvaluateFromString(sym.Value, rootVariableCollection);
                        bool currentValue;
                        stable &= computed.TryGetValue(symbol.Key, out currentValue) && currentValue == value;
                        rootVariableCollection[symbol.Key] = value;
                        computed[symbol.Key] = value;
                    }
                }
            }

            // evaluate the file glob (specials) conditions
            // the result is needed for SpecialOperationConfig
            foreach (ICustomFileGlobModel fileGlobModel in SpecialCustomSetup)
            {
                fileGlobModel.EvaluateCondition(rootVariableCollection);
            }

            foreach (ExtendedFileSource source in Sources)
            {
                if (!string.IsNullOrEmpty(source.Condition) && !CppStyleEvaluatorDefinition.EvaluateFromString(source.Condition, rootVariableCollection))
                {
                    continue;
                }

                List<string> includePattern = JTokenToCollection(source.Include, SourceFile, new[] { "**/*" }).ToList();
                List<string> excludePattern = JTokenToCollection(source.Exclude, SourceFile, new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" }).ToList();
                List<string> copyOnlyPattern = JTokenToCollection(source.CopyOnly, SourceFile, new[] { "**/node_modules/**/*" }).ToList();

                if (source.Modifiers != null)
                {
                    foreach (SourceModifier modifier in source.Modifiers)
                    {
                        if (string.IsNullOrEmpty(modifier.Condition) || CppStyleEvaluatorDefinition.EvaluateFromString(modifier.Condition, rootVariableCollection))
                        {
                            includePattern.AddRange(JTokenToCollection(modifier.Include, SourceFile, new string[0]));
                            excludePattern.AddRange(JTokenToCollection(modifier.Exclude, SourceFile, new string[0]));
                            copyOnlyPattern.AddRange(JTokenToCollection(modifier.CopyOnly, SourceFile, new string[0]));
                        }
                    }
                }

                Dictionary<string, string> renames = new Dictionary<string, string>();

                object resolvedValue;
                if (parameters.ResolvedValues.TryGetValue(NameParameter, out resolvedValue))
                {
                    foreach (IFileSystemInfo entry in configFile.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                    {
                        string tmpltRel = entry.PathRelativeTo(configFile.Parent);
                        string outRel = tmpltRel.Replace(SourceName, (string)resolvedValue);
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
                IReadOnlyList<string> includePattern = new[] { "**/*" };
                IReadOnlyList<string> excludePattern = new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" };
                IReadOnlyList<string> copyOnlyPattern = new[] { "**/node_modules/**/*" };

                Dictionary<string, string> renames = new Dictionary<string, string>();

                object resolvedValue;
                if (parameters.ResolvedValues.TryGetValue(NameParameter, out resolvedValue))
                {
                    foreach (IFileSystemInfo entry in configFile.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                    {
                        string tmpltRel = entry.PathRelativeTo(configFile.Parent);
                        string outRel = tmpltRel.Replace(SourceName, (string)resolvedValue);
                        renames[tmpltRel] = outRel;
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

        public static SimpleConfigModel FromJObject(JObject source)
        {
            SimpleConfigModel config = new SimpleConfigModel();
            config.Author = source.ToString(nameof(config.Author));
            config.Classifications = source.ArrayAsStrings(nameof(config.Classifications));
            config.DefaultName = source.ToString(nameof(DefaultName));
            config.GroupIdentity = source.ToString(nameof(GroupIdentity));
            config.Guids = source.ArrayAsGuids(nameof(config.Guids));
            config.Identity = source.ToString(nameof(config.Identity));
            config.Name = source.ToString(nameof(config.Name));
            config.ShortName = source.ToString(nameof(config.ShortName));
            config.SourceName = source.ToString(nameof(config.SourceName));

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

                List<SourceModifier> modifiers = new List<SourceModifier>();
                src.Modifiers = modifiers;
                foreach (JObject entry in item.Items<JObject>(nameof(src.Modifiers)))
                {
                    SourceModifier modifier = new SourceModifier();
                    modifier.Condition = entry.ToString(nameof(modifier.Condition));
                    modifier.CopyOnly = entry.Get<JToken>(nameof(modifier.CopyOnly));
                    modifier.Exclude = entry.Get<JToken>(nameof(modifier.Exclude));
                    modifier.Include = entry.Get<JToken>(nameof(modifier.Include));
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

                ISymbolModel model = SymbolModelConverter.GetModelForObject(obj);

                if (model != null)
                {
                    symbols[prop.Name] = model;
                }
            }

            config.Tags = source.ToStringDictionary(StringComparer.OrdinalIgnoreCase, nameof(config.Tags));
            config.PostActionModel = RunnableProjects.PostActionModel.ListFromJArray((JArray)source["PostActions"]);

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

            return config;
        }
    }
}