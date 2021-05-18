// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectTemplate : ITemplate
    {
        private readonly JObject _raw;
        private readonly IRunnableProjectConfig _config;
        private readonly IFile _configFile;
        private readonly IGenerator _generator;
        private readonly IFile _localeConfigFile;
        private readonly IFile _hostConfigFile;

        internal RunnableProjectTemplate(
            JObject raw,
            IGenerator generator,
            IFile configFile,
            IRunnableProjectConfig config,
            IFile localeConfigFile,
            IFile hostConfigFile)
        {
            _config = config;
            _configFile = configFile;
            _generator = generator;
            _localeConfigFile = localeConfigFile;
            _hostConfigFile = hostConfigFile;
            _raw = raw;
            config.SourceFile = configFile;
        }

        IDirectory ITemplate.TemplateSourceRoot
        {
            get
            {
                return _configFile?.Parent?.Parent;
            }
        }

        string ITemplateInfo.Identity => _config.Identity ?? _config.Name;

        Guid ITemplateInfo.GeneratorId => _generator.Id;

        string ITemplateInfo.Author => _config.Author;

        string ITemplateInfo.Description => _config.Description;

        IReadOnlyList<string> ITemplateInfo.Classifications => _config.Classifications;

        string ITemplateInfo.DefaultName => _config.DefaultName;

        IGenerator ITemplate.Generator => _generator;

        string ITemplateInfo.GroupIdentity => _config.GroupIdentity;

        int ITemplateInfo.Precedence => _config.Precedence;

        string ITemplateInfo.Name => _config.Name;

        [Obsolete]
        string ITemplateInfo.ShortName
        {
            get
            {
                if (((ITemplateInfo)this).ShortNameList.Count > 0)
                {
                    return ((ITemplateInfo)this).ShortNameList[0];
                }

                return string.Empty;
            }
        }

        IReadOnlyList<string> ITemplateInfo.ShortNameList => _config.ShortNameList ?? new List<string>();

        [Obsolete]
        IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags
        {
            get
            {
                Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
                foreach (KeyValuePair<string, string> tag in ((ITemplateInfo)this).TagsCollection)
                {
                    tags[tag.Key] = new CacheTag(null, null, new Dictionary<string, ParameterChoice> { { tag.Value, new ParameterChoice(null, null) } }, tag.Value);
                }
                foreach (ITemplateParameter parameter in ((ITemplateInfo)this).Parameters.Where(p => p.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase)))
                {
                    tags[parameter.Name] = new CacheTag(parameter.DisplayName, parameter.Description, parameter.Choices, parameter.DefaultValue);
                }
                return tags;
            }
        }

        [Obsolete]
        IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters
        {
            get
            {
                Dictionary<string, ICacheParameter> cacheParameters = new Dictionary<string, ICacheParameter>();
                foreach (ITemplateParameter parameter in ((ITemplateInfo)this).Parameters.Where(p => !p.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase)))
                {
                    cacheParameters[parameter.Name] = new CacheParameter()
                    {
                        DataType = parameter.DataType,
                        DefaultValue = parameter.DefaultValue,
                        Description = parameter.Documentation,
                        DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue,
                        DisplayName = parameter.DisplayName

                    };
                }
                return cacheParameters;
            }
        }

        IReadOnlyList<ITemplateParameter> ITemplateInfo.Parameters
        {
            get
            {
                return _config.Parameters.Values
                    .Where(param => param.Type.Equals("parameter", StringComparison.OrdinalIgnoreCase)
                        && param.Priority != TemplateParameterPriority.Implicit)
                    .ToList();
            }
        }

        IFileSystemInfo ITemplate.Configuration => _configFile;

        string ITemplateInfo.MountPointUri => _configFile.MountPoint.MountPointUri;

        string ITemplateInfo.ConfigPlace => _configFile.FullPath;

        IFileSystemInfo ITemplate.LocaleConfiguration => _localeConfigFile;

        string ITemplateInfo.LocaleConfigPlace => _localeConfigFile.FullPath;

        bool ITemplate.IsNameAgreementWithFolderPreferred => _raw.ToBool("preferNameDirectory", false);

        string ITemplateInfo.HostConfigPlace => _hostConfigFile?.FullPath;

        string ITemplateInfo.ThirdPartyNotices => _raw.ToString("thirdPartyNotices");

        IReadOnlyDictionary<string, IBaselineInfo> ITemplateInfo.BaselineInfo => _config.BaselineInfo;

        IReadOnlyDictionary<string, string> ITemplateInfo.TagsCollection => _config.Tags;

        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        internal IRunnableProjectConfig Config => _config;

        internal IFile ConfigFile => _configFile;
    }
}
