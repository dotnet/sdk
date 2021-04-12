using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    [Obsolete("The class is deprecated.")]
    public class FilterableTemplateInfo : ITemplateInfo, IShortNameList
    {
        public static FilterableTemplateInfo FromITemplateInfo(ITemplateInfo source)
        {
            FilterableTemplateInfo filterableTemplate = new FilterableTemplateInfo()
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
                Tags = source.Tags,
                CacheParameters = source.CacheParameters,
                Parameters = source.Parameters,
                MountPointUri = source.MountPointUri,
                ConfigPlace = source.ConfigPlace,
                LocaleConfigPlace = source.LocaleConfigPlace,
                HostConfigPlace = source.HostConfigPlace,
                ThirdPartyNotices = source.ThirdPartyNotices,
                BaselineInfo = source.BaselineInfo,
                HasScriptRunningPostActions = source.HasScriptRunningPostActions,
                ShortNameList = source.ShortNameList
            };

            return filterableTemplate;
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

        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; }

        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; }

        public IReadOnlyList<ITemplateParameter> Parameters { get; private set; }

        public string MountPointUri { get; private set; }

        public string ConfigPlace { get; private set; }

        public string LocaleConfigPlace { get; private set; }

        public string HostConfigPlace { get; private set; }

        public string ThirdPartyNotices { get; private set; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; }

        public bool HasScriptRunningPostActions { get; set; }

        public DateTime? ConfigTimestampUtc { get; set; }
    }
}
