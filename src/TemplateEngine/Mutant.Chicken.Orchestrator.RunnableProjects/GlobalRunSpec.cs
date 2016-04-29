using System;
using System.Collections.Generic;
using System.Text;
using Mutant.Chicken.Abstractions;
using Mutant.Chicken.Expressions.Cpp;
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

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public GlobalRunSpec(FileSource source, IParameterSet parameters, IReadOnlyDictionary<string, JObject> operations, IReadOnlyDictionary<string, Dictionary<string, JObject>> special)
        {
            List<IPathMatcher> includes = new List<IPathMatcher>(source.Include.Length);
            foreach (string include in source.Include)
            {
                includes.Add(new GlobbingPatternMatcher(include));
            }
            Include = includes;

            List<IPathMatcher> excludes = new List<IPathMatcher>(source.Exclude.Length);
            foreach (string exclude in source.Exclude)
            {
                excludes.Add(new GlobbingPatternMatcher(exclude));
            }
            Exclude = excludes;

            VariableCollection variables;
            Operations = ProcessOperations(parameters, operations, null, out variables);
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
                        specialOps = ProcessOperations(parameters, specialEntry.Value, variables, out specialVariables);
                    }

                    RunSpec spec = new RunSpec(specialOps, specialVariables ?? variables);
                    specials[new GlobbingPatternMatcher(specialEntry.Key)] = spec;
                }
            }

            Special = specials;
        }

        private IReadOnlyList<IOperationProvider> ProcessOperations(IParameterSet parameters, IReadOnlyDictionary<string, JObject> operations, VariableCollection parentVars, out VariableCollection variables)
        {
            List<IOperationProvider> result = new List<IOperationProvider>();
            VariableCollection vc = VariableCollection.Root();
            JObject variablesSection = operations["variables"];

            foreach (KeyValuePair<string, JObject> config in operations)
            {
                JObject data = config.Value;
                switch (config.Key)
                {
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
                    case "replacements":
                        foreach (JProperty property in data.Properties())
                        {
                            //TODO: Handle macros
                            ITemplateParameter param;
                            if (parameters.TryGetParameter(property.Value.ToString(), out param))
                            {
                                string val = parameters.ParameterValues[param];
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

        private VariableCollection HandleVariables(IParameterSet parameters, JObject data, List<IOperationProvider> result)
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
                        c = ProduceUserVariablesCollection(parameters, format);

                        if (fallbackFormat != null)
                        {
                            VariableCollection d = ProduceUserVariablesCollection(parameters, fallbackFormat);
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
                case "evaluate":
                    HandleEvaluateAction(variableName, variablesSection, def, set, result);
                    break;
                case "constant":
                    HandleConstantAction(variableName, def, set, result);
                    break;
            }
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
            VariableCollection vars = HandleVariables(parameters, variablesSection, null);
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

        private VariableCollection ProduceUserVariablesCollection(IParameterSet parameters, string format)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter parameter in parameters.Parameters)
            {
                Parameter param = (Parameter)parameter;
                if (param.IsVariable)
                {
                    string value = null;

                    if (!parameters.ParameterValues.TryGetValue(param, out value))
                    {
                        value = param.DefaultValue;
                    }

                    string key = string.Format(format ?? "{0}", param.Name);
                    vc[key] = value;
                }
            }

            return vc;
        }
    }
}