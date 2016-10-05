using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectGenerator : IGenerator
    {
        private static readonly Guid GeneratorId = new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        public Guid Id => GeneratorId;

        public Task Create(ITemplate templateData, IParameterSet parameters, IComponentManager componentManager, out ICreationResult creationResult)
        {
            RunnableProjectTemplate template = (RunnableProjectTemplate)templateData;
            ProcessMacros(componentManager, template.Config.OperationConfig, parameters);

            IVariableCollection variables = VariableCollection.SetupVariables(parameters, template.Config.OperationConfig.VariableSetup);
            template.Config.Evaluate(parameters, variables, template.ConfigFile);

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator();
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            GlobalRunSpec runSpec = new GlobalRunSpec(template.ConfigFile.Parent, componentManager, parameters, variables, template.Config.OperationConfig, template.Config.SpecialOperationConfig, template.Config.LocalizationOperations, template.Config.PlaceholderFilename);

            foreach (FileSource source in template.Config.Sources)
            {
                runSpec.SetupFileSource(source);
                string target = Path.Combine(Directory.GetCurrentDirectory(), source.Target);
                orchestrator.Run(runSpec, template.ConfigFile.Parent.DirectoryInfo(source.Source), target);
            }

            // todo: add anything else we'd want to report to the broker
            creationResult = new CreationResult()
            {
                PostActions = PostAction.ListFromModel(template.Config.PostActionModel, variables),
                PrimaryOutputs = CreationPath.ListFromModel(template.Config.PrimaryOutputs, variables)
            };

            return Task.FromResult(true);
        }

        // Note the deferred-config macros (generated) are part of the runConfig.Macros
        //      and not in the ComputedMacros.
        //  Possibly make a separate property for the deferred-config macros
        private static void ProcessMacros(IComponentManager componentManager, IGlobalRunConfig runConfig, IParameterSet parameters)
        {
            if (runConfig.Macros != null)
            {
                IVariableCollection varsForMacros = VariableCollection.SetupVariables(parameters, runConfig.VariableSetup);
                MacrosOperationConfig macroProcessor = new MacrosOperationConfig();
                macroProcessor.ProcessMacros(componentManager, runConfig.Macros, varsForMacros, parameters);
            }

            if (runConfig.ComputedMacros != null)
            {
                IVariableCollection varsForMacros = VariableCollection.SetupVariables(parameters, runConfig.VariableSetup);
                MacrosOperationConfig macroProcessor = new MacrosOperationConfig();
                macroProcessor.ProcessMacros(componentManager, runConfig.ComputedMacros, varsForMacros, parameters);
            }
        }

        public IParameterSet GetParametersForTemplate(ITemplate template)
        {
            RunnableProjectTemplate tmplt = (RunnableProjectTemplate)template;
            return new ParameterSet(tmplt.Config);
        }

        public IEnumerable<ITemplate> GetTemplatesFromSource(IMountPoint source)
        {
            return GetTemplatesFromDir(source.Root).ToList();
        }

        public bool TryGetTemplateFromConfig(IFileSystemInfo config, out ITemplate template, string localizationFile = null)
        {
            IFile file = config as IFile;

            if (file == null)
            {
                template = null;
                return false;
            }

            // notes for correctly reading localization info - not from .netnew.json - coming soon
            // this method is called when the cache is populated, as well as when actually reading a template for processing.
            // so we'll have to decide what/if to do regarding localization for setting up the cache, as opposed to regular processing.
            //
            //try
            //{
            //    JObject srcObject = ReadConfigModel(file);
            //    // read the neutral locale, decide if we need a locale-specific config
            //    // if need locale-specific
            //        // read the locale specific
            //        // JObject localeSrcObject =  ???()
            //    // else
            //        // JObject localeSrcObject = null;
                    
            //    // template = new RunnableProjectTemplate(srcObject, this, file, SimpleConfigModel.FromJObject(srcObject, localeSrcObject));
            //}

            try
            {
                JObject srcObject = ReadConfigModel(file);
                template = new RunnableProjectTemplate(srcObject, this, file, SimpleConfigModel.FromJObject(srcObject, localizationFile));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Error reading the template: {0}", ex.ToString()));
            }

            template = null;
            return false;
        }

        private JObject ReadConfigModel(IFile file)
        {
            using (Stream s = file.OpenRead())
            using (TextReader tr = new StreamReader(s, true))
            using (JsonReader r = new JsonTextReader(tr))
            {
                return JObject.Load(r);
            }
        }

        private IEnumerable<ITemplate> GetTemplatesFromDir(IDirectory folder)
        {
            foreach (IFile file in folder.EnumerateFiles(".netnew.json", SearchOption.AllDirectories))
            {
                ITemplate tmp;
                if (TryGetTemplateFromConfig(file, out tmp))
                {
                    yield return tmp;
                }
            }
        }

        public bool TryGetTemplateFromSource(IMountPoint target, string name, out ITemplate template)
        {
            template = GetTemplatesFromSource(target).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return template != null;
        }

        //
        // Converts the raw, string version of a parameter to a strongly typed value.
        // If the param has a datatype specified, use that. Otherwise attempt to infer the type.
        // Throws a TemplateParamException if the conversion fails for any reason.
        //
        public object ConvertParameterValueToType(ITemplateParameter parameter, string untypedValue)
        {
            return InternalConvertParameterValueToType(parameter, untypedValue);
        }

        internal static object InternalConvertParameterValueToType(ITemplateParameter parameter, string untypedValue)
        { 
            if (untypedValue == null)
            {
                throw new TemplateParamException("Parameter value is null", parameter.Name, null, parameter.DataType);
            }

            if (!string.IsNullOrEmpty(parameter.DataType))
            {
                object convertedValue = DataTypeSpecifiedConvertLiteral(parameter, untypedValue);

                if (convertedValue == null)
                {
                    throw new TemplateParamException("Parameter value could not be converted", parameter.Name, untypedValue, parameter.DataType);
                }

                return convertedValue;
            }
            else
            {
                return InferTypeAndConvertLiteral(untypedValue);
            }
        }

        // For explicitly data-typed variables, attempt to convert the variable value to the specified type.
        // Data type names:
        //     - choice
        //     - bool
        //     - float
        //     - int
        //     - hex
        //     - text
        // The data type names are case insensitive.
        //
        // Returns the converted value if it can be converted, throw otherwise
        internal static object DataTypeSpecifiedConvertLiteral(ITemplateParameter param, string literal)
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

        internal static object InferTypeAndConvertLiteral(string literal)
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


        internal class ParameterSet : IParameterSet
        {
            private readonly IDictionary<string, ITemplateParameter> _parameters = new Dictionary<string, ITemplateParameter>(StringComparer.OrdinalIgnoreCase);

            public ParameterSet(IRunnableProjectConfig config)
            {
                foreach (KeyValuePair<string, Parameter> p in config.Parameters)
                {
                    p.Value.Name = p.Key;
                    _parameters[p.Key] = p.Value;
                }
            }

            public IEnumerable<ITemplateParameter> ParameterDefinitions => _parameters.Values;

            public IDictionary<ITemplateParameter, object> ResolvedValues { get; } = new Dictionary<ITemplateParameter, object>();

            public IEnumerable<string> RequiredBrokerCapabilities => Enumerable.Empty<string>();

            public void AddParameter(ITemplateParameter param)
            {
                _parameters[param.Name] = param;
            }

            public bool TryGetParameterDefinition(string name, out ITemplateParameter parameter)
            {
                if (_parameters.TryGetValue(name, out parameter))
                {
                    return true;
                }

                parameter = new Parameter
                {
                    Name = name,
                    Requirement = TemplateParameterPriority.Optional,
                    IsVariable = true,
                    Type = "string"
                };

                return true;
            }
        }
    }
}
