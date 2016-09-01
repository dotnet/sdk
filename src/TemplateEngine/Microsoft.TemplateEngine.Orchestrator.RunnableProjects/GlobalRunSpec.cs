using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly IReadOnlyList<IOperationConfig> OperationConfigReaders;

        static GlobalRunSpec()
        {
            List<IOperationConfig> operationConfigReaders = new List<IOperationConfig>
            {
                new ConditionalConfig(),
                new FlagsConfig(),
                new IncludeConfig(),
                new MacrosConfig(),
                new RegionConfig(),
                new ReplacementConfig()
            };

            operationConfigReaders.Sort((x, y) => x.Order.CompareTo(y.Order));
            OperationConfigReaders = operationConfigReaders;
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

        public GlobalRunSpec(FileSource source, IDirectory templateRoot, IParameterSet parameters, IReadOnlyDictionary<string, JObject> operations, IReadOnlyDictionary<string, Dictionary<string, JObject>> special)
        {
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
            Operations = ProcessOperations(parameters, templateRoot, operations, out variables);
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
                        specialOps = ProcessOperations(parameters, templateRoot, specialEntry.Value, out specialVariables);
                    }

                    RunSpec spec = new RunSpec(specialOps, specialVariables ?? variables);
                    specials[new GlobbingPatternMatcher(specialEntry.Key)] = spec;
                }
            }

            Special = specials;
        }

        private static IReadOnlyList<IOperationProvider> ProcessOperations(IParameterSet parameters, IDirectory templateRoot, IReadOnlyDictionary<string, JObject> operations, out IVariableCollection variables)
        {
            List<IOperationProvider> result = new List<IOperationProvider>();
            JObject variablesSection = operations["variables"];

            foreach (IOperationConfig configReader in OperationConfigReaders)
            {
                JObject data;
                if (operations.TryGetValue(configReader.Key, out data))
                {
                    IVariableCollection vars = HandleVariables(parameters, variablesSection, null, true);
                    result.AddRange(configReader.Process(data, templateRoot, vars, (RunnableProjectGenerator.ParameterSet) parameters));
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

        private static VariableCollection ProduceUserVariablesCollection(IParameterSet parameters, string format, bool allParameters)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter parameter in parameters.ParameterDefinitions)
            {
                Parameter param = (Parameter)parameter;
                if (allParameters || param.IsVariable)
                {
                    string value;
                    string key = string.Format(format ?? "{0}", param.Name);
                    bool valueGetResult = parameters.ResolvedValues.TryGetValue(param, out value);

                    if (value == null)
                    {
                        throw new TemplateParamException("Parameter value is null", param.Name, null, param.DataType);
                    }

                    if (!string.IsNullOrEmpty(param.DataType))
                    {
                        object convertedValue = DataTypeSpecifiedConvertLiteral(param, value);

                        if (convertedValue == null)
                        {
                            throw new TemplateParamException("Parameter value could not be converted", param.Name, value, param.DataType);
                        }

                        vc[key] = convertedValue;
                    }
                    else
                    {
                        vc[key] = InferTypeAndConvertLiteral(value);
                    }
                }
            }

            return vc;
        }

        /// For explicitly data-typed variables, attempt to convert the variable value to the specified type.
        /// Data type names:
        ///     - choice
        ///     - bool
        ///     - float
        ///     - int
        ///     - hex
        ///     - text
        /// The data type names are case insensitive.
        ///
        /// Returns the converted value if it can be converted, throw otherwise
        private static object DataTypeSpecifiedConvertLiteral(Parameter param, string literal)
        {
            if (string.Equals(param.DataType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                else
                {
                    // Note: if the literal is ever null, it is probably due to a problem in TemplateCreator.Instantiate()
                    // which takes care of making null bool -> true as appropriate.
                    // This else can also happen if there is a value but it can't be converted.
                    throw new TemplateParamException("Value is not a bool", param.Name, literal, param.DataType);
                }
            }
            else if (string.Equals(param.DataType, "choice", StringComparison.OrdinalIgnoreCase))
            {
                if ((literal != null) && param.Choices.Contains(literal))
                {
                    return literal;
                }
                else
                {
                    string conversionErrorMessage = string.Format("Choice is invalid. Valid choices are: [{0}]", string.Join(",", param.Choices));
                    throw new TemplateParamException(conversionErrorMessage, param.Name, literal, param.DataType);
                }
            }
            else if (string.Equals(param.DataType, "float", StringComparison.OrdinalIgnoreCase))
            {
                double convertedFloat;
                if (double.TryParse(literal, out convertedFloat))
                {
                    return convertedFloat;
                }
                else
                {
                    throw new TemplateParamException("Value is not a float", param.Name, literal, param.DataType);
                }
            }
            else if (string.Equals(param.DataType, "int", StringComparison.OrdinalIgnoreCase))
            {
                long convertedInt;
                if (long.TryParse(literal, out convertedInt))
                {
                    return convertedInt;
                }
                else
                {
                    throw new TemplateParamException("Value is not an int", param.Name, literal, param.DataType);
                }
            }
            else if (string.Equals(param.DataType, "hex", StringComparison.OrdinalIgnoreCase))
            {
                long convertedHex;
                if (long.TryParse(literal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out convertedHex))
                {
                    return convertedHex;
                }
                else
                {
                    throw new TemplateParamException("Value is not hex format", param.Name, literal, param.DataType);
                }
            }
            else if (string.Equals(param.DataType, "text", StringComparison.OrdinalIgnoreCase))
            {   // "text" is a valid data type, but doesn't need any special handling.
                return literal;
            }
            else
            {
                string customMessage = string.Format("Param name = [{0}] had unknown data type = [{1}]", param.Name, param.DataType);
                throw new TemplateParamException(customMessage, param.Name, literal, param.DataType);
            }
        }

        private static object InferTypeAndConvertLiteral(string literal)
        {
            if (literal == null)
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

                if (string.Equals("null", literal, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return literal;
            }

            return literal.Substring(1, literal.Length - 2);
        }
    }
}