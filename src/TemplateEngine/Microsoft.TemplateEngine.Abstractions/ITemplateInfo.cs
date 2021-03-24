using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateInfo
    {
        string Author { get; }

        string Description { get; }

        IReadOnlyList<string> Classifications { get; }

        string DefaultName { get; }

        string Identity { get; }

        Guid GeneratorId { get; }

        string GroupIdentity { get; }

        int Precedence { get; }

        string Name { get; }

        string ShortName { get; }

        IReadOnlyDictionary<string, ICacheTag> Tags { get; }

        IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; }

        IReadOnlyList<ITemplateParameter> Parameters { get; }

        string MountPointUri { get; }

        string ConfigPlace { get; }

        string LocaleConfigPlace { get; }

        string HostConfigPlace { get; }

        string ThirdPartyNotices { get; }

        IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; }

        bool HasScriptRunningPostActions { get; set; }
    }
}
