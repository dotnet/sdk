using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
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
        private Dictionary<string, Dictionary<string, JObject>> _special;
        private Parameter _nameParameter;
        private string _safeNameName;

        public IFile SourceFile { get; set; }

        public string Author { get; set; }

        public IReadOnlyList<string> Classifications { get; set; }

        public string DefaultName { get; set; }

        public string GroupIdentity { get; set; }

        public IReadOnlyList<Guid> Guids { get; set; }

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

        public string ShortName { get; set; }

        public string SourceName { get; set; }

        public IReadOnlyList<ExtendedFileSource> Sources { get; set; }

        public IReadOnlyDictionary<string, ISymbolModel> Symbols { get; set; }

        public IReadOnlyDictionary<string, string> Tags { get; set; }

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

        private readonly Dictionary<Guid, string> _guidToGuidPrefixMap = new Dictionary<Guid, string>();

        IReadOnlyDictionary<string, JObject> IRunnableProjectConfig.Config => ProduceConfig("//", "//", true);

        IReadOnlyDictionary<string, string> IRunnableProjectConfig.Tags => Tags;

        IReadOnlyList<string> IRunnableProjectConfig.Classifications => Classifications;

        public string Identity { get; set; }

        public IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, IVariableCollection rootVariableCollection, IFile configFile, IOperationProvider[] operations)
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

            public IFile SourceFile
            {
                private get { return _simpleConfigModel.SourceFile; }
                set { _simpleConfigModel.SourceFile = value; }
            }

            public IReadOnlyList<FileSource> Sources => _sources;

            public IReadOnlyDictionary<string, Dictionary<string, JObject>> Special => ((IRunnableProjectConfig)_simpleConfigModel).Special;

            public IReadOnlyDictionary<string, string> Tags => _simpleConfigModel.Tags;

            public IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, IVariableCollection rootVariableCollection, IFile configFile, IOperationProvider[] providers)
            {
                return _simpleConfigModel.ReprocessWithParameters(parameters, rootVariableCollection, configFile, providers);
            }

            internal void Evaluate(IParameterSet parameters, IVariableCollection rootVariableCollection, IFileSystemInfo configFile)
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
                        foreach(IFileSystemInfo entry in configFile.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                        {
                            string tmpltRel = entry.PathRelativeTo(configFile.Parent);
                            string outRel = tmpltRel.Replace(_simpleConfigModel.SourceName, val);
                            renames[tmpltRel] = outRel;
                            //Console.WriteLine($"Mapping {tmpltRel} -> {outRel}");
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

                if (_simpleConfigModel.Sources.Count == 0)
                {
                    IReadOnlyList<string> includePattern = new[] { "**/*" };
                    IReadOnlyList<string> excludePattern = new[] { "/[Bb]in/", "/[Oo]bj/", ".netnew.json", "**/*.filelist" };
                    IReadOnlyList<string> copyOnlyPattern = new[] { "**/node_modules/**/*" };

                    Dictionary<string, string> renames = new Dictionary<string, string>();
                    // Console.WriteLine(_simpleConfigModel.NameParameter);

                    string val;
                    if (parameters.ParameterValues.TryGetValue(_simpleConfigModel.NameParameter, out val))
                    {
                        foreach (IFileSystemInfo entry in configFile.Parent.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                        {
                            string tmpltRel = entry.PathRelativeTo(configFile.Parent);
                            string outRel = tmpltRel.Replace(_simpleConfigModel.SourceName, val);
                            renames[tmpltRel] = outRel;
                            //Console.WriteLine($"Mapping {tmpltRel} -> {outRel}");
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
        }

        public static SimpleConfigModel FromJObject(JObject source)
        {
            SimpleConfigModel tmp = new SimpleConfigModel();
            tmp.Author = source.ToString(nameof(tmp.Author));
            tmp.Classifications = source.ArrayAsStrings(nameof(tmp.Classifications));
            tmp.DefaultName = source.ToString(nameof(DefaultName));
            tmp.GroupIdentity = source.ToString(nameof(GroupIdentity));
            tmp.Guids = source.ArrayAsGuids(nameof(tmp.Guids));
            tmp.Identity = source.ToString(nameof(tmp.Identity));
            tmp.Name = source.ToString(nameof(tmp.Name));
            tmp.ShortName = source.ToString(nameof(tmp.ShortName));
            tmp.SourceName = source.ToString(nameof(tmp.SourceName));

            List<ExtendedFileSource> sources = new List<ExtendedFileSource>();
            tmp.Sources = sources;

            foreach (JObject item in source.Items<JObject>(nameof(tmp.Sources)))
            {
                ExtendedFileSource src = new ExtendedFileSource();
                sources.Add(src);
                src.CopyOnly = item.Get<JToken>(nameof(src.CopyOnly));
                src.Exclude = item.Get<JToken>(nameof(src.Exclude));
                src.Include = item.Get<JToken>(nameof(src.Include));

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
            tmp.Symbols = symbols;
            foreach (JProperty prop in source.PropertiesOf(nameof(tmp.Symbols)))
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

            tmp.Tags = source.ToStringDictionary(StringComparer.OrdinalIgnoreCase, nameof(tmp.Tags));

            return tmp;
        }
    }
}