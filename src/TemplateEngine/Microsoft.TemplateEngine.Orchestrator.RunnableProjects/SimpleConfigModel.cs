using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class SimpleConfigModel : IRunnableProjectConfig
    {
        public SimpleConfigModel()
        {
            Sources = new[] { new ExtendedFileSource() };
        }

        [JsonIgnore]
        private IReadOnlyDictionary<string, Parameter> _parameters;

        [JsonIgnore]
        private IReadOnlyList<FileSource> _sources;

        [JsonIgnore]
        private Dictionary<string, Dictionary<string, JObject>> _special;

        [JsonIgnore]
        private Parameter _nameParameter;

        [JsonIgnore]
        private string _safeNameName;

        [JsonIgnore]
        public ITemplateSourceFile SourceFile { get; set; }

        [JsonProperty]
        public string Author { get; set; }

        [JsonProperty]
        public List<string> Classifications { get; set; }

        [JsonProperty]
        public string DefaultName { get; set; }

        [JsonProperty]
        public string GroupIdentity { get; set; }

        [JsonProperty]
        public List<Guid> Guids { get; set; }

        [JsonProperty]
        public string Name { get; set; }

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
                                    Type = param.Type
                                };
                            }
                        }
                    }

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

        [JsonProperty]
        public string ShortName { get; set; }

        [JsonProperty]
        public string SourceName { get; set; }

        [JsonProperty]
        public ExtendedFileSource[] Sources { get; set; }

        [JsonProperty(ItemConverterType = typeof(SymbolModelConverter))]
        public Dictionary<string, ISymbolModel> Symbols { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Tags { get; set; }

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
                        string[] includePattern = JTokenToCollection(source.Include, SourceFile, new[] { "**/*" });
                        string[] excludePattern = JTokenToCollection(source.Exclude, SourceFile, new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" });
                        string[] copyOnlyPattern = JTokenToCollection(source.CopyOnly, SourceFile, new[] { "**/node_modules/**/*" });

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

                    _sources = sources;
                }

                return _sources;
            }
        }

        IReadOnlyDictionary<string, Dictionary<string, JObject>> IRunnableProjectConfig.Special
        {
            get
            {
                if (_special == null)
                {
                    Dictionary<string, Dictionary<string, JObject>> specials =
                        new Dictionary<string, Dictionary<string, JObject>>
                        {
                            ["**/*.css"] = ProduceConfig("/* ", "/* ", false),
                            ["**/*.css.min"] = ProduceConfig("/* ", "/* ", false),
                            ["**/*.cs"] = ProduceConfig("//", "#", false),
                            ["**/*.cpp"] = ProduceConfig("//", "#", false),
                            ["**/*.hpp"] = ProduceConfig("//", "#", false),
                            ["**/*.h"] = ProduceConfig("//", "#", false),
                            ["**/*.*proj"] = ProduceConfig("<!--/", "<!--#", false),
                            ["**/*.*html"] = ProduceConfig("<!--", "<!--#", false),
                            ["**/*.*htm"] = ProduceConfig("<!--", "<!--#", false),
                            ["**/*.jsp"] = ProduceConfig("<!--", "<!--#", false),
                            ["**/*.asp"] = ProduceConfig("<!--", "<!--#", false),
                            ["**/*.aspx"] = ProduceConfig("<!--", "<!--#", false)
                        };



                    _special = specials;
                }

                return _special;
            }
        }

        [JsonIgnore]
        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new Dictionary<Guid, string>();

        [JsonIgnore]
        IReadOnlyDictionary<string, JObject> IRunnableProjectConfig.Config => ProduceConfig("//", "//", true);

        [JsonIgnore]
        IReadOnlyDictionary<string, string> IRunnableProjectConfig.Tags => Tags;

        [JsonIgnore]
        IReadOnlyList<string> IRunnableProjectConfig.Classifications => Classifications;

        [JsonProperty]
        public string Identity { get; set; }

        public IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, VariableCollection rootVariableCollection, ITemplateSourceFile configFile, IOperationProvider[] operations)
        {
            EvaluatedSimpleConfig config = new EvaluatedSimpleConfig(this);
            config.Evaluate(parameters, rootVariableCollection, configFile);
            return config;
        }

        private Dictionary<string, JObject> ProduceConfig(string switchPrefix, string conditionalPrefix, bool generateMacros)
        {
            Dictionary<string, JObject> cfg = new Dictionary<string, JObject>
            {
                {
                    "flags",
                    JObject.Parse($@"{{
    ""conditionals"": {{
        ""on"": ""{switchPrefix}+:cnd"",
        ""off"": ""{switchPrefix}-:cnd""
    }},
    ""replacements"": {{
        ""on"": ""{switchPrefix}+:replacements"",
        ""off"": ""{switchPrefix}-:replacements""
    }},
    ""expandVariables"": {{
        ""on"": ""{switchPrefix}+:vars"",
        ""off"": ""{switchPrefix}-:vars""
    }},
    ""flags"": {{
        ""off"": ""{switchPrefix}-:flags""
    }},
    ""include"": {{
        ""on"": ""{switchPrefix}+:include"",
        ""off"": ""{switchPrefix}-:include""
    }}
}}")
                },
                {
                    "conditionals",
                    JObject.Parse($@"{{
    ""if"": ""{conditionalPrefix}if"",
    ""else"": ""{conditionalPrefix}else"",
    ""elseif"": ""{conditionalPrefix}elseif"",
    ""endif"": ""{conditionalPrefix}endif"",
    ""evaluator"": ""C++"",
    ""wholeLine"": true,
    ""trim"": true
}}")
                },
                {
                    "variables",
                    JObject.Parse(@"{
    ""sources"": {
        ""environment"": ""env_{0}"",
        ""user"": ""usr_{0}"",
    },
    ""order"": [ ""environment"", ""user"" ],
    ""fallbackFormat"": ""{0}"",
    ""expand"": false
}")
                },
                {
                    "replacements",
                    new JObject()
                }
            };

            if (generateMacros)
            {
                cfg["macros"] = new JObject();
            }

            if (generateMacros && Guids != null)
            {
                int guidCount = 0;
                foreach (Guid guid in Guids)
                {
                    JObject macros = cfg["macros"];
                    JObject macro = new JObject();
                    int id = guidCount++;
                    macros["guid" + id] = macro;
                    macro["type"] = "guid";
                    macro["action"] = "new";

                    _guidToGuidPrefixMap[guid] = "guid" + id;
                }
            }

            foreach (KeyValuePair<Guid, string> map in _guidToGuidPrefixMap)
            {
                foreach (char fmt in "ndbpxNDPBX")
                {
                    string rplc = char.IsUpper(fmt) ? map.Key.ToString(fmt.ToString()).ToUpperInvariant() : map.Key.ToString(fmt.ToString()).ToLowerInvariant();
                    cfg["replacements"][rplc] = map.Value + "-" + fmt;
                }
            }

            if (Symbols != null)
            {
                foreach (KeyValuePair<string, ISymbolModel> symbol in Symbols)
                {
                    if (symbol.Value.Binding == "safe_name")
                    {
                        _safeNameName = symbol.Key;
                    }

                    if (symbol.Value.Type == "computed" && generateMacros)
                    {
                        JObject cmp = new JObject();
                        cfg["macros"][symbol.Key] = cmp;
                        cmp["type"] = "evaluate";
                        cmp["action"] = ((ComputedSymbol)symbol.Value).Value;
                        cmp["evaluator"] = "C++";
                    }
                    else if (symbol.Value.Type == "generated" && generateMacros)
                    {
                        JObject gen = new JObject();
                        cfg["macros"][symbol.Key] = gen;
                        GeneratedSymbol gs = (GeneratedSymbol)symbol.Value;
                        gen["type"] = gs.Generator;

                        foreach (KeyValuePair<string, string> parameter in gs.Parameters)
                        {
                            gen[parameter.Key] = parameter.Value;
                        }
                    }

                    if (symbol.Value.Replaces != null)
                    {
                        cfg["replacements"][symbol.Value.Replaces] = symbol.Key;
                    }
                }
            }

            if (_safeNameName == null && generateMacros)
            {
                cfg["macros"]["safe_name"] = JObject.Parse(@"{
    ""type"": ""regex"",
    ""action"": ""replace"",
    ""source"": """ + NameParameter.Name + @""",
    ""steps"": [{
        ""regex"": ""\\W"",
        ""replacement"": ""_""
    }]
}");
                _safeNameName = "safe_name";
            }

            cfg["replacements"][SourceName] = _safeNameName;

            return cfg;
        }

        private static string[] JTokenToCollection(JToken token, ITemplateSourceFile sourceFile, string[] defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }
            else if (token.Type == JTokenType.String)
            {
                using (Stream excludeList = sourceFile.Parent.OpenFile(token.ToObject<string>()))
                using (TextReader reader = new StreamReader(excludeList, Encoding.UTF8, true, 4096, true))
                {
                    return reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            else
            {
                return token.ToObject<string[]>();
            }
        }

        private class EvaluatedSimpleConfig : IRunnableProjectConfig
        {
            private readonly SimpleConfigModel _simpleConfigModel;
            private IReadOnlyList<FileSource> _sources;

            public EvaluatedSimpleConfig(SimpleConfigModel simpleConfigModel)
            {
                _simpleConfigModel = simpleConfigModel;
            }

            public string Author => _simpleConfigModel.Author;

            public IReadOnlyList<string> Classifications => _simpleConfigModel.Classifications;

            public IReadOnlyDictionary<string, JObject> Config => ((IRunnableProjectConfig)_simpleConfigModel).Config;

            public string DefaultName => _simpleConfigModel.DefaultName ?? _simpleConfigModel.SourceName;

            public string GroupIdentity => _simpleConfigModel.GroupIdentity;

            public string Identity => _simpleConfigModel.Identity;

            public string Name => _simpleConfigModel.Name;

            public IReadOnlyDictionary<string, Parameter> Parameters => ((IRunnableProjectConfig)_simpleConfigModel).Parameters;

            public string ShortName => _simpleConfigModel.ShortName;

            public ITemplateSourceFile SourceFile
            {
                private get { return _simpleConfigModel.SourceFile; }
                set { _simpleConfigModel.SourceFile = value; }
            }

            public IReadOnlyList<FileSource> Sources => _sources;

            public IReadOnlyDictionary<string, Dictionary<string, JObject>> Special => ((IRunnableProjectConfig)_simpleConfigModel).Special;

            public IReadOnlyDictionary<string, string> Tags => _simpleConfigModel.Tags;

            public IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, VariableCollection rootVariableCollection, ITemplateSourceFile configFile, IOperationProvider[] providers)
            {
                return _simpleConfigModel.ReprocessWithParameters(parameters, rootVariableCollection, configFile, providers);
            }

            internal void Evaluate(IParameterSet parameters, VariableCollection rootVariableCollection, ITemplateSourceFile configFile)
            {
                List<FileSource> sources = new List<FileSource>();
                bool stable = _simpleConfigModel.Symbols == null;
                Dictionary<string, bool> computed = new Dictionary<string, bool>();

                while (!stable)
                {
                    stable = true;
                    foreach (KeyValuePair<string, ISymbolModel> symbol in _simpleConfigModel.Symbols)
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

                foreach (ExtendedFileSource source in _simpleConfigModel.Sources)
                {
                    List<string> includePattern = JTokenToCollection(source.Include, SourceFile, new[] { "**/*" }).ToList();
                    List<string> excludePattern = JTokenToCollection(source.Exclude, SourceFile, new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" }).ToList();
                    List<string> copyOnlyPattern = JTokenToCollection(source.CopyOnly, SourceFile, new[] { "**/node_modules/**/*" }).ToList();

                    if (source.Modifiers != null)
                    {
                        foreach (SourceModifier modifier in source.Modifiers)
                        {
                            if (CppStyleEvaluatorDefinition.EvaluateFromString(modifier.Condition, rootVariableCollection))
                            {
                                includePattern.AddRange(JTokenToCollection(modifier.Include, SourceFile, new string[0]));
                                excludePattern.AddRange(JTokenToCollection(modifier.Exclude, SourceFile, new string[0]));
                                copyOnlyPattern.AddRange(JTokenToCollection(modifier.CopyOnly, SourceFile, new string[0]));
                            }
                        }
                    }

                    Dictionary<string, string> renames = new Dictionary<string, string>();
                    // Console.WriteLine(_simpleConfigModel.NameParameter);

                    string val;
                    if (parameters.ParameterValues.TryGetValue(_simpleConfigModel.NameParameter, out val))
                    {
                        foreach(ITemplateSourceEntry entry in configFile.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                        {
                            string tmpltRel = entry.PathRelativeTo(configFile.Parent);
                            string outRel = tmpltRel.Replace(_simpleConfigModel.SourceName, val);
                            renames[tmpltRel] = outRel;
                            // Console.WriteLine($"Mapping {tmpltRel} -> {outRel}");
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

                _sources = sources;
            }
        }
    }
}