// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public partial class TemplateSearchData : ITemplateInfo
    {
        public TemplateSearchData(ITemplateInfo templateInfo, IDictionary<string, object>? data = null)
        {
            TemplateInfo = new BlobStorageTemplateInfo(templateInfo);
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        public IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();

        public string Identity => TemplateInfo.Identity;

        public string? GroupIdentity => TemplateInfo.GroupIdentity;

        public string Name => TemplateInfo.Name;

        public IReadOnlyList<string> ShortNameList => TemplateInfo.ShortNameList;

        public string? Author => TemplateInfo.Author;

        public string? Description => TemplateInfo.Description;

        public IReadOnlyList<string> Classifications => TemplateInfo.Classifications;

        public IReadOnlyDictionary<string, string> TagsCollection => TemplateInfo.TagsCollection;

        public IReadOnlyList<ITemplateParameter> Parameters => TemplateInfo.Parameters;

        public int Precedence => TemplateInfo.Precedence;

        public string? ThirdPartyNotices => TemplateInfo.ThirdPartyNotices;

#region implicit ITemplateInfo implementation
        string? ITemplateInfo.DefaultName => TemplateInfo.DefaultName;

        Guid ITemplateInfo.GeneratorId => TemplateInfo.GeneratorId;

        [Obsolete]
        string ITemplateInfo.ShortName => TemplateInfo.ShortName;

        [Obsolete]
        IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags => TemplateInfo.Tags;

        [Obsolete]
        IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters => TemplateInfo.CacheParameters;

        string ITemplateInfo.MountPointUri => TemplateInfo.MountPointUri;

        string ITemplateInfo.ConfigPlace => TemplateInfo.ConfigPlace;

        string? ITemplateInfo.LocaleConfigPlace => TemplateInfo.LocaleConfigPlace;

        string? ITemplateInfo.HostConfigPlace => TemplateInfo.HostConfigPlace;

        IReadOnlyDictionary<string, IBaselineInfo> ITemplateInfo.BaselineInfo => TemplateInfo.BaselineInfo;

        [Obsolete]
        bool ITemplateInfo.HasScriptRunningPostActions { get => TemplateInfo.HasScriptRunningPostActions; set => throw new NotImplementedException(); }
        #endregion

        private ITemplateInfo TemplateInfo { get; }
    }
}
