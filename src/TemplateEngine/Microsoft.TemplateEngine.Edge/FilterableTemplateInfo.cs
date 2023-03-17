// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Edge
{
    [Obsolete("This class is deprecated.")]
    public class FilterableTemplateInfo : ITemplateInfo, IShortNameList
    {
        private readonly ITemplateInfo _source;

        public FilterableTemplateInfo() { }

        private FilterableTemplateInfo(ITemplateInfo source)
        {
            _source = source;
        }

        public string Author { get; private set; }

        public string Description { get; private set; }

        public IReadOnlyList<string> Classifications { get; private set; }

        public string DefaultName { get; private set; }

        public string Identity { get; private set; }

        public Guid GeneratorId { get; private set; }

        public string GroupIdentity { get; private set; }

        public int Precedence { get; private set; }

        public string Name { get; private set; }

        public string ShortName { get; private set; }

        public IReadOnlyList<string> ShortNameList { get; set; }

        public IReadOnlyList<string> GroupShortNameList { get; set; }

        public bool PreferDefaultName { get; private set; }

        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; }

        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; }

        public IParameterDefinitionSet ParameterDefinitions { get; private set; }

        [Obsolete("Use ParameterDefinitionSet instead.")]
        public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

        public string MountPointUri { get; private set; }

        public string ConfigPlace { get; private set; }

        public string LocaleConfigPlace { get; private set; }

        public string HostConfigPlace { get; private set; }

        public string ThirdPartyNotices { get; private set; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; }

        public bool HasScriptRunningPostActions { get; set; }

        public DateTime? ConfigTimestampUtc { get; set; }

        public IReadOnlyDictionary<string, string> TagsCollection { get; private set; }

        IReadOnlyList<Guid> ITemplateMetadata.PostActions => _source?.PostActions ?? Array.Empty<Guid>();

        IReadOnlyList<TemplateConstraintInfo> ITemplateMetadata.Constraints => _source?.Constraints ?? Array.Empty<TemplateConstraintInfo>();

        public static FilterableTemplateInfo FromITemplateInfo(ITemplateInfo source)
        {
            FilterableTemplateInfo filterableTemplate = new FilterableTemplateInfo(source)
            {
                Author = source.Author,
                Description = source.Description,
                Classifications = source.Classifications,
                DefaultName = source.DefaultName,
                Identity = source.Identity,
                GeneratorId = source.GeneratorId,
                GroupIdentity = source.GroupIdentity,
                Precedence = source.Precedence,
                Name = source.Name,
                ShortName = source.ShortName,
                PreferDefaultName = source.PreferDefaultName,
                Tags = source.Tags,
                CacheParameters = source.CacheParameters,
                ParameterDefinitions = source.ParameterDefinitions,
                MountPointUri = source.MountPointUri,
                ConfigPlace = source.ConfigPlace,
                LocaleConfigPlace = source.LocaleConfigPlace,
                HostConfigPlace = source.HostConfigPlace,
                ThirdPartyNotices = source.ThirdPartyNotices,
                BaselineInfo = source.BaselineInfo,
                HasScriptRunningPostActions = source.HasScriptRunningPostActions,
                ShortNameList = source.ShortNameList,
                TagsCollection = source.TagsCollection,
            };

            return filterableTemplate;
        }
    }
}
