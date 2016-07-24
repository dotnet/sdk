using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectTemplate : ITemplate
    {
        private readonly JObject _raw;

        public RunnableProjectTemplate(JObject raw, IGenerator generator, IFile configFile, IRunnableProjectConfig config)
        {
            config.SourceFile = configFile;
            ConfigFile = configFile;
            Generator = generator;
            Source = configFile.MountPoint;
            Config = config;
            DefaultName = config.DefaultName;
            Name = config.Name;
            Identity = config.Identity ?? config.Name;
            ShortName = config.ShortName;
            Author = config.Author;
            Tags = config.Tags;
            Classifications = config.Classifications;
            GroupIdentity = config.GroupIdentity;
            _raw = raw;
        }

        public string Identity { get; }

        public Guid GeneratorId => Generator.Id;

        public string Author { get; }

        public IReadOnlyList<string> Classifications { get; }

        public IRunnableProjectConfig Config { get; private set; }

        public IFile ConfigFile { get; private set; }

        public string DefaultName { get; }

        public IGenerator Generator { get; }

        public string GroupIdentity { get; }

        public string Name { get; }

        public string ShortName { get; }

        public IMountPoint Source { get; }

        public IReadOnlyDictionary<string, string> Tags { get; }

        public Guid ConfigMountPointId => Configuration.MountPoint.Info.MountPointId;

        public string ConfigPlace => Configuration.FullPath;

        public IFileSystemInfo Configuration => ConfigFile;

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