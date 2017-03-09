using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateInfo : ITemplateInfo
    {
        public TemplateInfo()
        {
        }

        private static readonly Func<JObject, TemplateInfo> _defaultReader;
        private static readonly IReadOnlyDictionary<string, Func<JObject, TemplateInfo>> _infoVersionReaders;

        static TemplateInfo()
        {
            Dictionary<string, Func<JObject, TemplateInfo>> versionReaders = new Dictionary<string, Func<JObject, TemplateInfo>>();
            versionReaders.Add("1.0.0.0", TemplateInfoReaderVersion1_0_0_0.FromJObject);
            _infoVersionReaders = versionReaders;

            _defaultReader = TemplateInfoReaderInitialVersion.FromJObject;
        }

        public static TemplateInfo FromJObject(JObject entry, string cacheVersion)
        {
            Func<JObject, TemplateInfo> infoReader;

            if (string.IsNullOrEmpty(cacheVersion) || !_infoVersionReaders.TryGetValue(cacheVersion, out infoReader))
            {
                infoReader = _defaultReader;
            }

            return infoReader(entry);
        }

        [JsonProperty]
        public Guid ConfigMountPointId { get; set; }

        [JsonProperty]
        public string Author { get; set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; set; }

        [JsonProperty]
        public string DefaultName { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public string Identity { get; set; }

        [JsonProperty]
        public Guid GeneratorId { get; set; }

        [JsonProperty]
        public string GroupIdentity { get; set; }

        [JsonProperty]
        public int Precedence { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string ShortName { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheTag> Tags { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; set; }

        [JsonProperty]
        public string ConfigPlace { get; set; }

        [JsonProperty]
        public Guid LocaleConfigMountPointId { get; set; }

        [JsonProperty]
        public string LocaleConfigPlace { get; set; }

        [JsonProperty]
        public Guid HostConfigMountPointId { get; set; }

        [JsonProperty]
        public string HostConfigPlace { get; set; }
    }
}
