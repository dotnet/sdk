using System;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Orchestrator.RunnableProjects
{
    public class RunnableProjectTemplate : ITemplate
    {
        private JObject _raw;

        public RunnableProjectTemplate(JObject raw, IGenerator generator, IConfiguredTemplateSource source, ITemplateSourceFile configFile, ConfigModel config)
        {
            ConfigFile = configFile;
            Generator = generator;
            Source = source;
            Config = config;
            DefaultName = config.DefaultName ?? config.Name;
            Name = config.Name;
            ShortName = config.ShortName;
            _raw = raw;
        }

        public ConfigModel Config { get; private set; }

        public ITemplateSourceFile ConfigFile { get; private set; }

        public string DefaultName { get; }

        public IGenerator Generator { get; }

        public string Name { get; }

        public string ShortName { get; }

        public IConfiguredTemplateSource Source { get; }

        public bool TryGetProperty(string name, out string value)
        {
            JToken token;
            if(_raw.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out token))
            {
                value = token?.ToString();
                return true;
            }

            value = null;
            return false;
        }
    }
}
