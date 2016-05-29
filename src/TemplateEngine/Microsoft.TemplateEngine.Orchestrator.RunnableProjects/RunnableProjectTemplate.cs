using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectTemplate : ITemplate
    {
        private JObject _raw;

        public RunnableProjectTemplate(JObject raw, IGenerator generator, IConfiguredTemplateSource source, ITemplateSourceFile configFile, IRunnableProjectConfig config)
        {
            config.SourceFile = configFile;
            ConfigFile = configFile;
            Generator = generator;
            Source = source;
            Config = config;
            DefaultName = config.DefaultName ?? config.Name;
            Name = config.Name;
            ShortName = config.ShortName;
            Author = config.Author;
            Tags = config.Tags;
            Classifications = config.Classifications;
            GroupIdentity = config.GroupIdentity;
            _raw = raw;
        }

        public string Author { get; }

        public IReadOnlyList<string> Classifications { get; }

        public IRunnableProjectConfig Config { get; private set; }

        public ITemplateSourceFile ConfigFile { get; private set; }

        public string DefaultName { get; }

        public IGenerator Generator { get; }

        public string GroupIdentity { get; }

        public string Name { get; }

        public string ShortName { get; }

        public IConfiguredTemplateSource Source { get; }

        public IReadOnlyDictionary<string, string> Tags { get; }

        public bool TryGetProperty(string name, out string value)
        {
            JToken token;
            if (_raw.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out token))
            {
                value = token?.ToString();
                return true;
            }

            value = null;
            return false;
        }
    }
}