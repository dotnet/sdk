using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GlobalRunSpec : IGlobalRunSpec
    {
        private static IReadOnlyList<IOperationConfig> _operationConfigReaders;

        private static void SetupOperations(IComponentManager componentManager)
        {
            if (_operationConfigReaders == null)
            {
                List<IOperationConfig> operationConfigReaders = componentManager.OfType<IOperationConfig>().ToList();
                operationConfigReaders.Sort((x, y) => x.Order.CompareTo(y.Order));
                _operationConfigReaders = operationConfigReaders;
            }
        }

        public IReadOnlyList<IPathMatcher> Exclude { get; }

        public IReadOnlyList<IPathMatcher> Include { get; }

        public IReadOnlyList<IOperationProvider> Operations { get; }

        public IVariableCollection RootVariableCollection { get; }

        public IReadOnlyDictionary<IPathMatcher, IRunSpec> Special { get; }

        public IReadOnlyList<IPathMatcher> CopyOnly { get; }

        public IReadOnlyDictionary<string, string> Rename { get; }

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return Rename.TryGetValue(sourceRelPath, out targetRelPath);
        }

        public GlobalRunSpec(FileSource source, IDirectory templateRoot, IParameterSet parameters, IReadOnlyDictionary<string, JObject> operations, IReadOnlyDictionary<string, Dictionary<string, JObject>> special, IComponentManager componentManager)
        {
            SetupOperations(componentManager);

            int expect = source.Include?.Count ?? 0;
            List<IPathMatcher> includes = new List<IPathMatcher>(expect);
            if (source.Include != null && expect > 0)
            {
                foreach (string include in source.Include)
                {
                    includes.Add(new GlobbingPatternMatcher(include));
                }
            }
            Include = includes;

            expect = source.CopyOnly?.Count ?? 0;
            List<IPathMatcher> copyOnlys = new List<IPathMatcher>(expect);
            if (source.CopyOnly != null && expect > 0)
            {
                foreach (string copyOnly in source.CopyOnly)
                {
                    copyOnlys.Add(new GlobbingPatternMatcher(copyOnly));
                }
            }
            CopyOnly = copyOnlys;

            expect = source.Exclude?.Count ?? 0;
            List<IPathMatcher> excludes = new List<IPathMatcher>(expect);
            if (source.Exclude != null && expect > 0)
            {
                foreach (string exclude in source.Exclude)
                {
                    excludes.Add(new GlobbingPatternMatcher(exclude));
                }
            }
            Exclude = excludes;

            Rename = source.Rename ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            IVariableCollection variables;
            Operations = ProcessOperations(componentManager, parameters, templateRoot, operations, out variables);
            RootVariableCollection = variables;
            Dictionary<IPathMatcher, IRunSpec> specials = new Dictionary<IPathMatcher, IRunSpec>();

            if (special != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, JObject>> specialEntry in special)
                {
                    IReadOnlyList<IOperationProvider> specialOps = null;
                    IVariableCollection specialVariables = variables;

                    if (specialEntry.Value != null)
                    {
                        specialOps = ProcessOperations(componentManager, parameters, templateRoot, specialEntry.Value, out specialVariables);
                    }

                    RunSpec spec = new RunSpec(specialOps, specialVariables ?? variables);
                    specials[new GlobbingPatternMatcher(specialEntry.Key)] = spec;
                }
            }

            Special = specials;
        }

        private static IReadOnlyList<IOperationProvider> ProcessOperations(IComponentManager componentManager, IParameterSet parameters, IDirectory templateRoot, IReadOnlyDictionary<string, JObject> operations, out IVariableCollection variables)
        {
            List<IOperationProvider> result = new List<IOperationProvider>();
            JObject variablesSection = operations["variables"];

            foreach (IOperationConfig configReader in _operationConfigReaders)
            {
                JObject data;
                if (operations.TryGetValue(configReader.Key, out data))
                {
                    IVariableCollection vars = HandleVariables(parameters, variablesSection, null, true);
                    result.AddRange(configReader.Process(componentManager, data, templateRoot, vars, (RunnableProjectGenerator.ParameterSet) parameters));
                }
            }

            variables = HandleVariables(parameters, variablesSection, result);
            return result;
        }

        private static IVariableCollection HandleVariables(IParameterSet parameters, JObject data, List<IOperationProvider> result, bool allParameters = false)
        {
            IVariableCollection vc = VariableCollection.Root();
            JToken expandToken;
            if (data.TryGetValue("expand", out expandToken) && expandToken.Type == JTokenType.Boolean && expandToken.ToObject<bool>())
            {
                result?.Add(new ExpandVariables(null));
            }

            JObject sources = (JObject)data["sources"];
            string fallbackFormat = data.ToString("fallbackFormat");
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
                IVariableCollection current = collections[order.ToString()];

                IVariableCollection tmp = current;
                while (tmp.Parent != null)
                {
                    tmp = tmp.Parent;
                }

                tmp.Parent = vc;
                vc = current;
            }

            return vc;
        }

        private static VariableCollection ProduceUserVariablesCollection(IParameterSet parameters, string format, bool allParameters)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter parameter in parameters.ParameterDefinitions)
            {
                Parameter param = (Parameter)parameter;
                if (allParameters || param.IsVariable)
                {
                    object value;
                    string key = string.Format(format ?? "{0}", param.Name);

                    if (!parameters.ResolvedValues.TryGetValue(param, out value))
                    {
                        throw new TemplateParamException("Parameter value was not specified", param.Name, null, param.DataType);
                    }
                    else if (value == null)
                    {
                        throw new TemplateParamException("Parameter value is null", param.Name, null, param.DataType);
                    }
                    else
                    {
                        vc[key] = value;
                    }
                }
            }

            return vc;
        }

        internal class ProcessorState : IProcessorState
        {
            public ProcessorState(IVariableCollection vars, byte[] buffer, Encoding encoding)
            {
                Config = new EngineConfig(vars);
                CurrentBuffer = buffer;
                CurrentBufferPosition = 0;
                Encoding = encoding;
                EncodingConfig = new EncodingConfig(Config, encoding);
            }

            public IEngineConfig Config { get; }

            public byte[] CurrentBuffer { get; private set; }

            public int CurrentBufferLength => CurrentBuffer.Length;

            public int CurrentBufferPosition { get; }

            public Encoding Encoding { get; set; }

            public IEncodingConfig EncodingConfig { get; }

            public bool AdvanceBuffer(int bufferPosition)
            {
                byte[] tmp = new byte[CurrentBufferLength - bufferPosition];
                Buffer.BlockCopy(CurrentBuffer, bufferPosition, tmp, 0, CurrentBufferLength - bufferPosition);
                CurrentBuffer = tmp;

                return true;
            }

            public void SeekBackUntil(ITokenTrie match)
            {
                throw new NotImplementedException();
            }

            public void SeekBackUntil(ITokenTrie match, bool consume)
            {
                throw new NotImplementedException();
            }

            public void SeekBackWhile(ITokenTrie match)
            {
                throw new NotImplementedException();
            }

            public void SeekForwardUntil(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition)
            {
                throw new NotImplementedException();
            }

            public void SeekForwardThrough(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition)
            {
                throw new NotImplementedException();
            }

            public void SeekForwardWhile(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition)
            {
                throw new NotImplementedException();
            }
        }
    }
}