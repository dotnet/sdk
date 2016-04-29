using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Orchestrator.RunnableProjects
{
    public class RunnableProjectGenerator : IGenerator
    {
        public string Name => "Runnable Project";

        public Task Create(ITemplate template, IParameterSet parameters)
        {
            RunnableProjectTemplate tmplt = (RunnableProjectTemplate)template;
            ParameterSet p = (ParameterSet)parameters;
            ITemplateParameter projectNameParameter = p.Parameters.FirstOrDefault(x => x.IsName);

            Dictionary<string, string> fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> copyOnly = new List<string>();

            RunnableProjectOrchestrator o = new RunnableProjectOrchestrator();

            //TODO: Reprocess config, reload, use when configuring the global specs below
            //o.Run(new ReprocessConfigGlobalRunSpec(tmplt.Config,))

            foreach (FileSource source in tmplt.Config.Sources)
            {
                GlobalRunSpec runSpec = new GlobalRunSpec(source, p, tmplt.Config.Config, tmplt.Config.Special);
                string target = Path.Combine(Directory.GetCurrentDirectory(), source.Target);
                o.Run(runSpec, tmplt.ConfigFile.Parent, target);
            }

            return Task.FromResult(true);
        }

        public IParameterSet GetParametersForTemplate(ITemplate template)
        {
            RunnableProjectTemplate t = (RunnableProjectTemplate)template;
            return new ParameterSet(t.Config);
        }

        public IEnumerable<ITemplate> GetTemplatesFromSource(IConfiguredTemplateSource source)
        {
            using (IDisposable<ITemplateSourceFolder> root = source.Root)
            {
                return GetTemplatesFromDir(source, root.Value).ToList();
            }
        }

        private JObject ReadConfigModel(ITemplateSourceFile file)
        {
            using (Stream s = file.OpenRead())
            using (TextReader tr = new StreamReader(s, true))
            using (JsonReader r = new JsonTextReader(tr))
            {
                return JObject.Load(r);
            }
        }

        private IEnumerable<ITemplate> GetTemplatesFromDir(IConfiguredTemplateSource source, ITemplateSourceFolder folder)
        {
            foreach (ITemplateSourceEntry entry in folder.Children)
            {
                if (entry.Kind == TemplateSourceEntryKind.File && entry.FullPath.EndsWith(".netnew.config"))
                {
                    RunnableProjectTemplate tmp = null;
                    try
                    {
                        ITemplateSourceFile file = (ITemplateSourceFile)entry;
                        JObject srcObject = ReadConfigModel(file);

                        tmp = new RunnableProjectTemplate(srcObject, this, source, file, srcObject.ToObject<ConfigModel>());
                    }
                    catch
                    {
                    }

                    if (tmp != null)
                    {
                        yield return tmp;
                    }
                }
                else if (entry.Kind == TemplateSourceEntryKind.Folder)
                {
                    foreach (ITemplate template in GetTemplatesFromDir(source, (ITemplateSourceFolder)entry))
                    {
                        yield return template;
                    }
                }
            }
        }

        public bool TryGetTemplateFromSource(IConfiguredTemplateSource target, string name, out ITemplate template)
        {
            template = GetTemplatesFromSource(target).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return template != null;
        }

        internal class ParameterSet : IParameterSet
        {
            private readonly IDictionary<string, ITemplateParameter> _parameters = new Dictionary<string, ITemplateParameter>(StringComparer.OrdinalIgnoreCase);

            public ParameterSet(ConfigModel config)
            {
                foreach(KeyValuePair<string, Parameter> p in config.Parameters)
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
