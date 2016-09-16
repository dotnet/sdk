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

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GlobalRunSpec : IGlobalRunSpec
    {
        //private static IReadOnlyList<IOperationConfig> _operationConfigReaders;

        //private static void SetupOperationConfigReaders(IComponentManager componentManager)
        //{
        //    if (_operationConfigReaders == null)
        //    {
        //        List<IOperationConfig> operationConfigReaders = componentManager.OfType<IOperationConfig>().ToList();
        //        operationConfigReaders.Sort((x, y) => x.Order.CompareTo(y.Order));
        //        _operationConfigReaders = operationConfigReaders;
        //    }
        //}

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

        public GlobalRunSpec(FileSource source, IDirectory templateRoot, IParameterSet parameters, 
            IComponentManager componentManager, 
            IGlobalRunConfig operations, 
            IReadOnlyDictionary<string, IGlobalRunConfig> specialOperations)
        {
            //SetupOperationConfigReaders(componentManager);
            Include = SetupPathInfoFromSource(source.Include);
            CopyOnly = SetupPathInfoFromSource(source.CopyOnly);
            Exclude = SetupPathInfoFromSource(source.Exclude);

            Rename = source.Rename ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // regular operations
            IVariableCollection variables;
            Operations = SetupOperations(componentManager, parameters, templateRoot, operations, out variables);
            RootVariableCollection = variables;
            Dictionary<IPathMatcher, IRunSpec> specials = new Dictionary<IPathMatcher, IRunSpec>();

            // special operations
            if (specialOperations != null)
            {
                foreach (KeyValuePair<string, IGlobalRunConfig> specialEntry in specialOperations)
                {
                    IReadOnlyList<IOperationProvider> specialOps = null;
                    IVariableCollection specialVariables = variables;

                    if (specialEntry.Value != null)
                    {
                        specialOps = SetupOperations(componentManager, parameters, templateRoot, specialEntry.Value, out specialVariables);
                    }

                    RunSpec spec = new RunSpec(specialOps, specialVariables ?? variables);
                    specials[new GlobbingPatternMatcher(specialEntry.Key)] = spec;
                }
            }

            Special = specials;
        }

        private static IReadOnlyList<IPathMatcher> SetupPathInfoFromSource(IReadOnlyList<string> fileSources)
        {
            int expect = fileSources?.Count ?? 0;
            List<IPathMatcher> paths = new List<IPathMatcher>(expect);
            if (fileSources != null && expect > 0)
            {
                foreach (string include in fileSources)
                {
                    paths.Add(new GlobbingPatternMatcher(include));
                }
            }

            return paths;
        }

        private static IReadOnlyList<IOperationProvider> SetupOperations(IComponentManager componentManager, IParameterSet parameters, IDirectory templateRoot, 
            IGlobalRunConfig runConfig, 
            out IVariableCollection variables)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();
            operations.AddRange(runConfig.Operations);

            // this loop will eventually get phased out of any SimpleConfigModel based processing - there will be nothing remaining that uses it
            // but stuff from ConfigModel will use it eventually - while refactoring its transition in config processing
            // ... after that it gets deleted.
            //foreach (IOperationConfig configReader in _operationConfigReaders)
            //{
            //    JObject data;
            //    if (operationConfig.TryGetValue(configReader.Key, out data))
            //    {
            //        IVariableCollection vars = SetupVariables(parameters, runConfig.VariableSetup, null, true);
            //        operations.AddRange(configReader.Process(componentManager, data, templateRoot, vars, (RunnableProjectGenerator.ParameterSet)parameters));
            //    }
            //}

            if (runConfig.Macros != null)
            {
                IVariableCollection varsForMacros = SetupVariables(parameters, runConfig.VariableSetup, null, true);
                MacrosOperationConfig macroProcessor = new MacrosOperationConfig();
                macroProcessor.Setup(componentManager, runConfig.Macros, varsForMacros, parameters);
            }

            if (runConfig.Replacements != null)
            {
                foreach (IReplacementTokens replaceSetup in runConfig.Replacements)
                {
                    IOperationProvider replacement = ReplacementConfig.Setup(replaceSetup, parameters);
                    if (replacement != null)
                    {
                        operations.Add(replacement);
                    }
                }
            }

            variables = SetupVariables(parameters, runConfig.VariableSetup, operations);
            return operations;
        }

        private static IVariableCollection SetupVariables(IParameterSet parameters, IVariableConfig variableConfig, List<IOperationProvider> operations, bool allParameters = false)
        {
            IVariableCollection variables = VariableCollection.Root();

            if (variableConfig.Expand)
            {
                operations?.Add(new ExpandVariables(null));
            }

            Dictionary<string, VariableCollection> collections = new Dictionary<string, VariableCollection>();

            foreach (KeyValuePair<string, string> source in variableConfig.Sources)
            {
                VariableCollection variablesForSource = null;
                string format = source.Value;

                switch (source.Key)
                {
                    case "environment":
                        variablesForSource = VariableCollection.Environment(format);

                        if (variableConfig.FallbackFormat != null)
                        {
                            variablesForSource = VariableCollection.Environment(variablesForSource, variableConfig.FallbackFormat);
                        }
                        break;
                    case "user":
                        variablesForSource = ProduceUserVariablesCollection(parameters, format, allParameters);

                        if (variableConfig.FallbackFormat != null)
                        {
                            VariableCollection variablesFallback = ProduceUserVariablesCollection(parameters, variableConfig.FallbackFormat, allParameters);
                            variablesFallback.Parent = variablesForSource;
                            variablesForSource = variablesFallback;
                        }
                        break;
                }

                collections[source.Key] = variablesForSource;
            }

            foreach (string order in variableConfig.Order)
            {
                IVariableCollection current = collections[order.ToString()];

                IVariableCollection tmp = current;
                while (tmp.Parent != null)
                {
                    tmp = tmp.Parent;
                }

                tmp.Parent = variables;
                variables = current;
            }

            return variables;
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