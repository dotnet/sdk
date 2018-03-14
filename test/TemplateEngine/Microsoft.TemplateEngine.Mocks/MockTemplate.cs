using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockTemplate : ITemplateInfo, IShortNameList
    {
        public MockTemplate()
        {
            ShortNameList = new List<string>();
        }

        public string Author { get; set; }

        public string Description { get; set; }

        public IReadOnlyList<string> Classifications { get; set; }

        public string DefaultName { get; set; }

        public string Identity { get; set; }

        public Guid GeneratorId { get; set; }

        public string GroupIdentity { get; set; }

        public int Precedence { get; set; }

        public string Name { get; set; }

        public string ShortName
        {
            get
            {
                if (ShortNameList.Count > 0)
                {
                    return ShortNameList[0];
                }

                return String.Empty;
            }
            set
            {
                if (ShortNameList.Count > 0)
                {
                    throw new Exception("Can't set the short name when the ShortNameList already has entries.");
                }

                ShortNameList = new List<string>() { value };
            }
        }

        public IReadOnlyList<string> ShortNameList { get; set; }

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
