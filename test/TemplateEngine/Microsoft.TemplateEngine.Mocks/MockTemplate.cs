using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockTemplate : ITemplateInfo
    {
        public string Author { get; set; }

        public string Description { get; set; }

        public IReadOnlyList<string> Classifications { get; set; }

        public string DefaultName { get; set; }

        public string Identity { get; set; }

        public Guid GeneratorId { get; set; }

        public string GroupIdentity { get; set; }

        public int Precedence { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public IReadOnlyDictionary<string, ICacheTag> Tags { get; set; }

        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; set; }

        public IReadOnlyList<ITemplateParameter> Parameters { get; set; }

        public Guid ConfigMountPointId { get; set; }

        public string ConfigPlace { get; set; }

        public Guid LocaleConfigMountPointId { get; set; }

        public string LocaleConfigPlace { get; set; }

        public Guid HostConfigMountPointId { get; set; }

        public string HostConfigPlace { get; set; }

        public string ThirdPartyNotices { get; set; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; set; }

        public bool HasScriptRunningPostActions { get; set; }
    }
}
