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
        IGenerator ITemplate.Generator => Generator;

        IFileSystemInfo ITemplate.Configuration => ConfigFile ?? throw new InvalidOperationException("Configuration file is not initialized, are you using test constructor?");

        string ITemplateLocator.MountPointUri => ConfigFile?.MountPoint.MountPointUri ?? throw new InvalidOperationException("Configuration file is not initialized, are you using test constructor?");

        string ITemplateLocator.ConfigPlace => ConfigFile?.FullPath ?? throw new InvalidOperationException("Configuration file is not initialized, are you using test constructor?");

        IFileSystemInfo? ITemplate.LocaleConfiguration => _localizationInfo?.File;

        IFileSystemInfo? ITemplate.HostSpecificConfiguration => _hostConfigFile;

        string? IExtendedTemplateLocator.LocaleConfigPlace => _localizationInfo?.File.FullPath;

        string? IExtendedTemplateLocator.HostConfigPlace => _hostConfigFile?.FullPath;

        bool ITemplate.IsNameAgreementWithFolderPreferred => ConfigurationModel.PreferNameDirectory;

        IDirectory ITemplate.TemplateSourceRoot => TemplateSourceRoot;

        string? ITemplateMetadata.Author => ConfigurationModel.Author;

        string? ITemplateMetadata.Description => ConfigurationModel.Description;

        IReadOnlyList<string> ITemplateMetadata.Classifications => ConfigurationModel.Classifications;

        string? ITemplateMetadata.DefaultName => ConfigurationModel.DefaultName;

        string ITemplateMetadata.Identity => ConfigurationModel.Identity;

        Guid ITemplateLocator.GeneratorId => Generator.Id;

        string? ITemplateMetadata.GroupIdentity => ConfigurationModel.GroupIdentity;

        int ITemplateMetadata.Precedence => ConfigurationModel.Precedence;

        string ITemplateMetadata.Name => ConfigurationModel.Name ?? throw new TemplateAuthoringException("Template configuration should have 'name' defined.", "name");

        IReadOnlyList<string> ITemplateMetadata.ShortNameList => ConfigurationModel.ShortNameList ?? new List<string>();

        IParameterDefinitionSet ITemplateMetadata.ParameterDefinitions => new ParameterDefinitionSet(ConfigurationModel.ExtractParameters());

        string? ITemplateMetadata.ThirdPartyNotices => ConfigurationModel.ThirdPartyNotices;

        IReadOnlyDictionary<string, IBaselineInfo> ITemplateMetadata.BaselineInfo => ConfigurationModel.BaselineInfo;

        IReadOnlyDictionary<string, string> ITemplateMetadata.TagsCollection => ConfigurationModel.Tags;

        IReadOnlyList<Guid> ITemplateMetadata.PostActions => ConfigurationModel.PostActionModels.Select(pam => pam.ActionId).ToArray();

        IReadOnlyList<TemplateConstraintInfo> ITemplateMetadata.Constraints => ConfigurationModel.Constraints;

        bool ITemplateMetadata.PreferDefaultName => ConfigurationModel.PreferDefaultName;

        ILocalizationLocator? ITemplate.Localization => Localization;

        #region Obsolete implementation

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

        [Obsolete("Use ParameterDefinitionSet instead.")]
        IReadOnlyList<ITemplateParameter> ITemplateInfo.Parameters => ParameterDefinitions;

        [Obsolete]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        #endregion

        void IDisposable.Dispose() => SourceMountPoint.Dispose();
    }
}
