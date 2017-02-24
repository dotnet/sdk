using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectTemplate : ITemplate
    {
        private readonly JObject _raw;

        public RunnableProjectTemplate(JObject raw, IGenerator generator, IFile configFile, IRunnableProjectConfig config, IFile localeConfigFile, IFile hostConfigFile)
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
            Tags = config.Tags ?? new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase);
            CacheParameters = config.CacheParameters ?? new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase);
            Description = config.Description;
            Classifications = config.Classifications;
            GroupIdentity = config.GroupIdentity;
            LocaleConfigFile = localeConfigFile;
            IsNameAgreementWithFolderPreferred = raw.ToBool("preferNameDirectory", false);
            HostConfigMountPointId = hostConfigFile?.MountPoint?.Info?.MountPointId ?? Guid.Empty;
            HostConfigPlace = hostConfigFile?.FullPath;
            _raw = raw;
        }

        public IDirectory TemplateSourceRoot
        {
            get
            {
                return ConfigFile.Parent.Parent;
            }
        }

        public string Identity { get; }

        public Guid GeneratorId => Generator.Id;

        public string Author { get; }

        public string Description { get; }

        public IReadOnlyList<string> Classifications { get; }

        public IRunnableProjectConfig Config { get; private set; }

        public string DefaultName { get; }

        public IGenerator Generator { get; }

        public string GroupIdentity { get; }

        public string Name { get; }

        public string ShortName { get; }

        public IMountPoint Source { get; }

        public IReadOnlyDictionary<string, ICacheTag> Tags { get; }

        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; }

        public IFile ConfigFile { get; }

        public IFileSystemInfo Configuration => ConfigFile;

        public Guid ConfigMountPointId => Configuration.MountPoint.Info.MountPointId;

        public string ConfigPlace => Configuration.FullPath;

        public IFile LocaleConfigFile { get; }

        public IFileSystemInfo LocaleConfiguration => LocaleConfigFile;

        public Guid LocaleConfigMountPointId => LocaleConfiguration.MountPoint.Info.MountPointId;

        public string LocaleConfigPlace => LocaleConfiguration.FullPath;

        public bool IsNameAgreementWithFolderPreferred { get; }

        public Guid HostConfigMountPointId { get; }

        public string HostConfigPlace { get; }
    }
}