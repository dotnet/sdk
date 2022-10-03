// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal partial class RunnableProjectConfig : ITemplate
    {
        IDirectory ITemplate.TemplateSourceRoot => TemplateSourceRoot;

        string ITemplateInfo.Identity => ConfigurationModel.Identity;

        Guid ITemplateInfo.GeneratorId => _generator.Id;

        string? ITemplateInfo.Author => ConfigurationModel.Author;

        string? ITemplateInfo.Description => ConfigurationModel.Description;

        IReadOnlyList<string> ITemplateInfo.Classifications => ConfigurationModel.Classifications;

        string? ITemplateInfo.DefaultName => ConfigurationModel.DefaultName;

        IGenerator ITemplate.Generator => _generator;

        string? ITemplateInfo.GroupIdentity => ConfigurationModel.GroupIdentity;

        int ITemplateInfo.Precedence => ConfigurationModel.Precedence;

        string ITemplateInfo.Name => ConfigurationModel.Name ?? throw new TemplateAuthoringException("Template configuration should have 'name' defined.", "name");

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

        IReadOnlyList<string> ITemplateInfo.ShortNameList => ConfigurationModel.ShortNameList ?? new List<string>();

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
                foreach (ITemplateParameter parameter in ((ITemplateInfo)this).ParameterDefinitions.Where(TemplateParameterExtensions.IsChoice))
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
                foreach (ITemplateParameter parameter in ((ITemplateInfo)this).ParameterDefinitions.Where(TemplateParameterExtensions.IsChoice))
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

        public IParameterDefinitionSet ParameterDefinitions => new ParameterDefinitionSet(ConfigurationModel.ExtractParameters());

        [Obsolete("Use ParameterDefinitionSet instead.")]
        public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

        IFileSystemInfo ITemplate.Configuration => SourceFile ?? throw new InvalidOperationException("Source file is not initialized, are you using test constructor?");

        string ITemplateInfo.MountPointUri => SourceFile?.MountPoint.MountPointUri ?? throw new InvalidOperationException("Source file is not initialized, are you using test constructor?");

        string ITemplateInfo.ConfigPlace => SourceFile?.FullPath ?? throw new InvalidOperationException("Source file is not initialized, are you using test constructor?");

        IFileSystemInfo? ITemplate.LocaleConfiguration => _localeConfigFile;

        string? ITemplateInfo.LocaleConfigPlace => _localeConfigFile?.FullPath;

        //read in simple template model instead
        bool ITemplate.IsNameAgreementWithFolderPreferred => ConfigurationModel.PreferNameDirectory;

        string? ITemplateInfo.HostConfigPlace => _hostConfigFile?.FullPath;

        //read in simple template model instead
        string? ITemplateInfo.ThirdPartyNotices => ConfigurationModel.ThirdPartyNotices;

        IReadOnlyDictionary<string, IBaselineInfo> ITemplateInfo.BaselineInfo => ConfigurationModel.BaselineInfo;

        IReadOnlyDictionary<string, string> ITemplateInfo.TagsCollection => ConfigurationModel.Tags;

        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        IReadOnlyList<Guid> ITemplateInfo.PostActions => ConfigurationModel.PostActionModels.Select(pam => pam.ActionId).ToArray();

        IReadOnlyList<TemplateConstraintInfo> ITemplateInfo.Constraints => ConfigurationModel.Constraints;
    }
}
