using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mutant.Chicken.Abstractions;
using Mutant.Chicken.Core;
using Mutant.Chicken.Core.Expressions.Cpp;
using Mutant.Chicken.Runner;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Orchestrator.RunnableProjects
{
    public class GlobalRunSpec : IGlobalRunSpec
    {
        public IReadOnlyList<IPathMatcher> Exclude { get; }

        public IReadOnlyList<IPathMatcher> Include { get; }

        public IReadOnlyList<IOperationProvider> Operations { get; }

        public VariableCollection RootVariableCollection { get; }

        public IReadOnlyDictionary<IPathMatcher, IRunSpec> Special { get; }

        public IReadOnlyList<IPathMatcher> CopyOnly { get; private set; }

        public IReadOnlyDictionary<string, string> Rename { get; private set; }

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return Rename.TryGetValue(sourceRelPath, out targetRelPath);
        }

        public GlobalRunSpec(FileSource source, ITemplateSourceFolder templateRoot, IParameterSet parameters, IReadOnlyDictionary<string, JObject> operations, IReadOnlyDictionary<string, Dictionary<string, JObject>> special)
        {
            int expect = source.Include?.Length ?? 0;
            List<IPathMatcher> includes = new List<IPathMatcher>(expect);
            if (expect > 0)
            {
                foreach (string include in source.Include)
                {
                    includes.Add(new GlobbingPatternMatcher(include));
                }
            }
            Include = includes;

            expect = source.CopyOnly?.Length ?? 0;
            List<IPathMatcher> copyOnlys = new List<IPathMatcher>(expect);
            if (expect > 0)
            {
                foreach (string copyOnly in source.CopyOnly)
                {
                    copyOnlys.Add(new GlobbingPatternMatcher(copyOnly));
                }
            }
            CopyOnly = copyOnlys;

            expect = source.Exclude?.Length ?? 0;
            List<IPathMatcher> excludes = new List<IPathMatcher>(expect);
            if (expect > 0)
            {
                foreach (string exclude in source.Exclude)
                {
                    excludes.Add(new GlobbingPatternMatcher(exclude));
                }
            }
            Exclude = excludes;

            if (source.Rename != null)
            {
                Rename = new Dictionary<string, string>(source.Rename, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                Rename = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            VariableCollection variables;
            Operations = ProcessOperations(parameters, templateRoot, operations, null, out variables);
            RootVariableCollection = variables;
            Dictionary<IPathMatcher, IRunSpec> specials = new Dictionary<IPathMatcher, IRunSpec>();

            if (special != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, JObject>> specialEntry in special)
                {
                    IReadOnlyList<IOperationProvider> specialOps = null;
                    VariableCollection specialVariables = variables;

                    if (specialEntry.Value != null)
                    {
                        specialOps = ProcessOperations(parameters, templateRoot, specialEntry.Value, variables, out specialVariables);
                    }

                    RunSpec spec = new RunSpec(specialOps, specialVariables ?? variables);
                    specials[new GlobbingPatternMatcher(specialEntry.Key)] = spec;
                }
            }

            Special = specials;
        }

        private IReadOnlyList<IOperationProvider> ProcessOperations(IParameterSet parameters, ITemplateSourceFolder templateRoot, IReadOnlyDictionary<string, JObject> operations, VariableCollection parentVars, out VariableCollection variables)
        {
            List<IOperationProvider> result = new List<IOperationProvider>();
            VariableCollection vc = VariableCollection.Root();
            JObject variablesSection = operations["variables"];

            foreach (KeyValuePair<string, JObject> config in operations)
            {
                JObject data = config.Value;
                switch (config.Key)
                {
                    case "include":
                        string startToken = data["start"].ToString();
                        string endToken = data["end"].ToString();
                        result.Add(new Include(startToken, endToken, path => templateRoot.OpenFile(path)));
                        break;
                    case "regions":
                        JArray regionSettings = (JArray)data["settings"];
                        foreach(JToken child in regionSettings.Children())
                        {
                            JObject setting = (JObject)child;
                            string start = setting["start"].ToString();
                            string end = setting["end"].ToString();
                            bool include = setting["include"]?.ToObject<bool>() ?? false;
                            bool regionTrim = setting["trim"]?.ToObject<bool>() ?? false;
                            bool regionWholeLine = setting["wholeLine"]?.ToObject<bool>() ?? false;
                            result.Add(new Region(start, end, include, regionWholeLine, regionTrim));
                        }
                        break;
                    case "conditionals":
                        string ifToken = data["if"].ToString();
                        string elseToken = data["else"].ToString();
                        string elseIfToken = data["elseif"].ToString();
                        string endIfToken = data["endif"].ToString();
                        string evaluatorName = data["evaluator"].ToString();
                        bool trim = data["trim"]?.ToObject<bool>() ?? false;
                        bool wholeLine = data["wholeLine"]?.ToObject<bool>() ?? false;
                        ConditionEvaluator evaluator = CppStyleEvaluatorDefinition.CppStyleEvaluator;

                        switch (evaluatorName)
                        {
                            case "C++":
                                evaluator = CppStyleEvaluatorDefinition.CppStyleEvaluator;
                                break;
                        }

                        result.Add(new Conditional(ifToken, elseToken, elseIfToken, endIfToken, wholeLine, trim, evaluator));
                        break;
                    case "flags":
                        foreach (JProperty property in data.Properties())
                        {
                            JObject innerData = (JObject)property.Value;
                            string flag = property.Name;
                            string on = innerData["on"]?.ToString() ?? string.Empty;
                            string off = innerData["off"]?.ToString() ?? string.Empty;
                            string onNoEmit = innerData["onNoEmit"]?.ToString() ?? string.Empty;
                            string offNoEmit = innerData["offNoEmit"]?.ToString() ?? string.Empty;
                            string defaultStr = innerData["default"]?.ToString();
                            bool? @default = null;

                            if (defaultStr != null)
                            {
                                @default = bool.Parse(defaultStr);
                            }

                            result.Add(new SetFlag(flag, on, off, onNoEmit, offNoEmit, @default));
                        }
                        break;
                    case "replacements":
                        foreach (JProperty property in data.Properties())
                        {
                            ITemplateParameter param;
                            if (parameters.TryGetParameter(property.Value.ToString(), out param))
                            {
                                string val = string.Empty;
                                try
                                {
                                    val = parameters.ParameterValues[param];
                                }
                                catch (KeyNotFoundException ex)
                                {
                                    throw new Exception($"Unable to find a parameter value called \"{param.Name}\"", ex);
                                }

                                Replacment r = new Replacment(property.Name, val);
                                result.Add(r);
                            }
                        }
                        break;
                    case "macros":
                        foreach (JProperty property in data.Properties())
                        {
                            RunMacros(property, variablesSection, parameters, result);
                        }
                        break;
                }
            }

            variables = HandleVariables(parameters, variablesSection, result);
            return result;
        }

        private VariableCollection HandleVariables(IParameterSet parameters, JObject data, List<IOperationProvider> result, bool allParameters = false)
        {
            VariableCollection vc = VariableCollection.Root();
            JToken expandToken;
            if (data.TryGetValue("expand", out expandToken) && expandToken.Type == JTokenType.Boolean && expandToken.ToObject<bool>())
            {
                result?.Add(new ExpandVariables());
            }

            JObject sources = (JObject)data["sources"];
            string fallbackFormat = data["fallbackFormat"]?.ToString();
            Dictionary<string, VariableCollection> collections = new Dictionary<string, VariableCollection>();

            foreach (JProperty prop in sources.Properties())
            {
                VariableCollection c = null;
                string format = prop.Value.ToString();

                switch (prop.Name)
                {
                    case "environment":
                        c = VariableCollection.Environment(format);

                        if (fallbackFormat != null)
                        {
                            c = VariableCollection.Environment(c, fallbackFormat);
                        }
                        break;
                    case "user":
                        c = ProduceUserVariablesCollection(parameters, format, allParameters);

                        if (fallbackFormat != null)
                        {
                            VariableCollection d = ProduceUserVariablesCollection(parameters, fallbackFormat, allParameters);
                            d.Parent = c;
                            c = d;
                        }
                        break;
                }

                collections[prop.Name] = c;
            }

            foreach (JToken order in ((JArray)data["order"]).Children())
            {
                VariableCollection current = collections[order.ToString()];

                VariableCollection tmp = current;
                while (tmp.Parent != null)
                {
                    tmp = tmp.Parent;
                }

                tmp.Parent = vc;
                vc = current;
            }

            return vc;
        }

        private void RunMacros(JProperty macro, JObject variablesSection, IParameterSet parameters, List<IOperationProvider> result)
        {
            RunnableProjectGenerator.ParameterSet set = (RunnableProjectGenerator.ParameterSet)parameters;
            string variableName = macro.Name;
            JObject def = (JObject)macro.Value;

            switch (def["type"].ToString())
            {
                case "guid":
                    HandleGuidAction(variableName, def, set, result);
                    break;
                case "now":
                    HandleNowAction(variableName, def, set, result);
                    break;
                case "evaluate":
                    HandleEvaluateAction(variableName, variablesSection, def, set, result);
                    break;
                case "constant":
                    HandleConstantAction(variableName, def, set, result);
                    break;
                case "regex":
                    HandleRegexAction(variableName, variablesSection, def, set, result);
                    break;
            }
        }

        private void HandleRegexAction(string variableName, JObject variablesSection, JObject def, RunnableProjectGenerator.ParameterSet parameters, List<IOperationProvider> result)
        {
            VariableCollection vars = HandleVariables(parameters, variablesSection, null, true);
            string action = def["action"]?.ToString();
            string value = null;

            switch (action)
            {
                case "replace":
                    string sourceVar = def["source"]?.ToString();
                    JArray steps = def["steps"] as JArray;
                    object working;
                    if(!vars.TryGetValue(sourceVar, out working))
                    {
                        ITemplateParameter param;
                        if(!parameters.TryGetParameter(sourceVar, out param) || !parameters.ParameterValues.TryGetValue(param, out value))
                        {
                            value = string.Empty;
                        }
                    }
                    else
                    {
                        value = working?.ToString() ?? "";
                    }

                    foreach(JToken child in steps)
                    {
                        JObject map = (JObject)child;
                        string regex = map["regex"]?.ToString();
                        string replaceWith = map["replacement"]?.ToString();

                        value = Regex.Replace(value, regex, replaceWith);
                    }
                    break;
            }

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            parameters.AddParameter(p);
            parameters.ParameterValues[p] = value;
        }

        private void HandleNowAction(string variableName, JObject def, RunnableProjectGenerator.ParameterSet parameters, List<IOperationProvider> result)
        {
            string format = def["action"]?.ToString();
            bool utc = bool.Parse(def["utc"]?.ToString() ?? "False");
            DateTime time = utc ? DateTime.UtcNow : DateTime.Now;
            string value = time.ToString(format);
            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            parameters.AddParameter(p);
            parameters.ParameterValues[p] = value;
        }

        private void HandleConstantAction(string variableName, JObject def, RunnableProjectGenerator.ParameterSet parameters, List<IOperationProvider> result)
        {
            string value = def["action"].ToString();
            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            parameters.AddParameter(p);
            parameters.ParameterValues[p] = value;
        }

        private void HandleEvaluateAction(string variableName, JObject variablesSection, JObject def, RunnableProjectGenerator.ParameterSet parameters, List<IOperationProvider> result)
        {
            ConditionEvaluator evaluator = CppStyleEvaluatorDefinition.CppStyleEvaluator;
            VariableCollection vars = HandleVariables(parameters, variablesSection, null, true);
            switch (def["evaluator"]?.ToString() ?? "C++")
            {
                case "C++":
                    evaluator = CppStyleEvaluatorDefinition.CppStyleEvaluator;
                    break;
            }

            byte[] data = Encoding.UTF8.GetBytes(def["action"].ToString());
            int len = data.Length;
            int pos = 0;
            IProcessorState state = new ProcessorState(vars, data, Encoding.UTF8);
            bool res = evaluator(state, ref len, ref pos);

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            parameters.AddParameter(p);
            parameters.ParameterValues[p] = res.ToString();
        }

        private class ProcessorState : IProcessorState
        {
            public ProcessorState(VariableCollection vars, byte[] buffer, Encoding encoding)
            {
                Config = new EngineConfig(vars);
                CurrentBuffer = buffer;
                CurrentBufferPosition = 0;
                Encoding = encoding;
                EncodingConfig = new EncodingConfig(Config, encoding);
            }

            public EngineConfig Config { get; }

            public byte[] CurrentBuffer { get; private set; }

            public int CurrentBufferLength => CurrentBuffer.Length;

            public int CurrentBufferPosition { get; private set; }

            public Encoding Encoding { get; set; }

            public EncodingConfig EncodingConfig { get; }

            public void AdvanceBuffer(int bufferPosition)
            {
                byte[] tmp = new byte[CurrentBufferLength - bufferPosition];
                Buffer.BlockCopy(CurrentBuffer, bufferPosition, tmp, 0, CurrentBufferLength - bufferPosition);
                CurrentBuffer = tmp;
            }

            public void SeekBackUntil(SimpleTrie match)
            {
                throw new NotImplementedException();
            }

            public void SeekBackUntil(SimpleTrie match, bool consume)
            {
                throw new NotImplementedException();
            }

            public void SeekBackWhile(SimpleTrie match)
            {
                throw new NotImplementedException();
            }

            public void SeekForwardThrough(SimpleTrie trie, ref int bufferLength, ref int currentBufferPosition)
            {
                throw new NotImplementedException();
            }

            public void SeekForwardWhile(SimpleTrie trie, ref int bufferLength, ref int currentBufferPosition)
            {
                throw new NotImplementedException();
            }
        }

        private void HandleGuidAction(string variableName, JObject def, RunnableProjectGenerator.ParameterSet parameters, List<IOperationProvider> result)
        {
            switch (def["action"].ToString())
            {
                case "new":
                    string value = Guid.NewGuid().ToString(def["format"]?.ToString() ?? "D");
                    Parameter p = new Parameter
                    {
                        IsVariable = true,
                        Name = variableName
                    };

                    parameters.AddParameter(p);
                    parameters.ParameterValues[p] = value;
                    break;
            }
        }

        private VariableCollection ProduceUserVariablesCollection(IParameterSet parameters, string format, bool allParameters)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter parameter in parameters.Parameters)
            {
                Parameter param = (Parameter)parameter;
                if (allParameters || param.IsVariable)
                {
                    string value = null;
                    if (parameters.ParameterValues.TryGetValue(param, out value))
                    {
                        string key = string.Format(format ?? "{0}", param.Name);
                        vc[key] = InferTypeAndConvertLiteral(value);
                    }
                }
            }

            return vc;
        }

        private static object InferTypeAndConvertLiteral(string literal)
        {
            if(literal == null)
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

                double literalDouble;
                if (literal.Contains(".") && double.TryParse(literal, out literalDouble))
                {
                    return literalDouble;
                }

                long literalLong;
                if (long.TryParse(literal, out literalLong))
                {
                    return literalLong;
                }

                if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(literal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out literalLong))
                {
                    return literalLong;
                }

                if(string.Equals("null", literal, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return literal;
            }

            return literal.Substring(1, literal.Length - 2);
        }
    }
}