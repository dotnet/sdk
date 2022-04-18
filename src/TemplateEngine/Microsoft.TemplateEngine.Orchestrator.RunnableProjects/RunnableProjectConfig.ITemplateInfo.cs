// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal partial class RunnableProjectConfig : ITemplate
    {
        IDirectory? ITemplate.TemplateSourceRoot => TemplateSourceRoot;

        string ITemplateInfo.Identity => _configuration.Identity ?? _configuration.Name ?? throw new TemplateValidationException("Template configuration should have name defined");

        Guid ITemplateInfo.GeneratorId => _generator.Id;

        string? ITemplateInfo.Author => _configuration.Author;

        string? ITemplateInfo.Description => _configuration.Description;

        IReadOnlyList<string> ITemplateInfo.Classifications => _configuration.Classifications;

        string? ITemplateInfo.DefaultName => _configuration.DefaultName;

        IGenerator ITemplate.Generator => _generator;

        string? ITemplateInfo.GroupIdentity => _configuration.GroupIdentity;

        int ITemplateInfo.Precedence => _configuration.Precedence;

        string ITemplateInfo.Name => _configuration.Name ?? throw new TemplateValidationException("Template configuration should have name defined");

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

        IReadOnlyList<string> ITemplateInfo.ShortNameList => _configuration.ShortNameList ?? new List<string>();

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
                foreach (ITemplateParameter parameter in ((ITemplateInfo)this).Parameters.Where(p => p.DataType != null && p.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase)))
                {
                    IReadOnlyDictionary<string, ParameterChoice> choices = parameter.Choices ?? new Dictionary<string, ParameterChoice>();
                    tags[parameter.Name] = new CacheTag(parameter.DisplayName, parameter.Description, choices, parameter.DefaultValue);
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
                foreach (ITemplateParameter parameter in ((ITemplateInfo)this).Parameters.Where(p => p.DataType != null && !p.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase)))
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
                return Parameters.Values
                    .Where(param => param.Type.Equals("parameter", StringComparison.OrdinalIgnoreCase)
                        && param.Priority != TemplateParameterPriority.Implicit)
                    .ToList();
            }
        }

        IFileSystemInfo ITemplate.Configuration => SourceFile;

        string ITemplateInfo.MountPointUri => SourceFile.MountPoint.MountPointUri;

        string ITemplateInfo.ConfigPlace => SourceFile.FullPath;

        IFileSystemInfo? ITemplate.LocaleConfiguration => _localeConfigFile;

        string? ITemplateInfo.LocaleConfigPlace => _localeConfigFile?.FullPath;

        //read in simple template model instead
        bool ITemplate.IsNameAgreementWithFolderPreferred => _configuration.PreferNameDirectory;

        string? ITemplateInfo.HostConfigPlace => _hostConfigFile?.FullPath;

        //read in simple template model instead
        string? ITemplateInfo.ThirdPartyNotices => _configuration.ThirdPartyNotices;

        IReadOnlyDictionary<string, IBaselineInfo> ITemplateInfo.BaselineInfo => _configuration.BaselineInfo;

        IReadOnlyDictionary<string, string> ITemplateInfo.TagsCollection => _configuration.Tags;

        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        IReadOnlyList<Guid> ITemplateInfo.PostActions => _configuration.PostActionModels.Select(pam => pam.ActionId).ToArray();

        IReadOnlyList<TemplateConstraintInfo> ITemplateInfo.Constraints => _configuration.Constraints;
    }
}
