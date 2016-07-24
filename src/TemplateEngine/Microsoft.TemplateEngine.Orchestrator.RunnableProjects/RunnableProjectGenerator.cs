using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Runner;
using Microsoft.TemplateEngine.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectGenerator : IGenerator
    {
        private static readonly Guid GeneratorId = new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

        public Guid Id => GeneratorId;

        public Task Create(IOrchestrator basicOrchestrator, ITemplate template, IParameterSet parameters)
        {
            //Console.ReadLine();
            RunnableProjectTemplate tmplt = (RunnableProjectTemplate)template;

            RunnableProjectOrchestrator o = new RunnableProjectOrchestrator(basicOrchestrator);
            GlobalRunSpec configRunSpec = new GlobalRunSpec(new FileSource(), tmplt.ConfigFile.Parent, parameters, tmplt.Config.Config, tmplt.Config.Special);
            IOperationProvider[] providers = configRunSpec.Operations.ToArray();

            foreach (KeyValuePair<IPathMatcher, IRunSpec> special in configRunSpec.Special)
            {
                if (special.Key.IsMatch(".netnew.json"))
                {
                    providers = special.Value.GetOperations(providers).ToArray();
                    break;
                }
            }

            IRunnableProjectConfig m = tmplt.Config.ReprocessWithParameters(parameters, configRunSpec.RootVariableCollection, tmplt.ConfigFile, providers);

            foreach (FileSource source in m.Sources)
            {
                GlobalRunSpec runSpec = new GlobalRunSpec(source, tmplt.ConfigFile.Parent, parameters, m.Config, m.Special);
                string target = Path.Combine(Directory.GetCurrentDirectory(), source.Target);
                o.Run(runSpec, tmplt.ConfigFile.Parent.DirectoryInfo(source.Source), target);
            }

            return Task.FromResult(true);
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

        public bool TryGetTemplateFromConfig(IFileSystemInfo config, out ITemplate template)
        {
            IFile file = config as IFile;

            if (file == null)
            {
                template = null;
                return false;
            }

            try
            {
                JObject srcObject = ReadConfigModel(file);

                template = new RunnableProjectTemplate(srcObject, this, file, RunnableProjectConfigConverter.FromJObject(srcObject));

                return true;
            }
            catch
            {
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

            public IEnumerable<ITemplateParameter> Parameters => _parameters.Values;

            public IDictionary<ITemplateParameter, string> ParameterValues { get; } = new Dictionary<ITemplateParameter, string>();

            public IEnumerable<string> RequiredBrokerCapabilities => Enumerable.Empty<string>();

            public void AddParameter(ITemplateParameter param)
            {
                _parameters[param.Name] = param;
            }

            public bool TryGetParameter(string name, out ITemplateParameter parameter)
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
