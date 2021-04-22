// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateInfo : ITemplateInfo
    {
        public static readonly string CurrentVersion = "1.0.0.5";

        private static readonly Func<JObject, TemplateInfo> _defaultReader = TemplateInfoReaderInitialVersion.FromJObject;

        // Note: Be sure to keep the versioning consistent with SettingsStore
        private static readonly IReadOnlyDictionary<string, Func<JObject, TemplateInfo>> _infoVersionReaders = new Dictionary<string, Func<JObject, TemplateInfo>>()
        {
            { "1.0.0.0", TemplateInfoReaderVersion1_0_0_0.FromJObject },
            { "1.0.0.1", TemplateInfoReaderVersion1_0_0_1.FromJObject },
            { "1.0.0.2", TemplateInfoReaderVersion1_0_0_2.FromJObject },
            { "1.0.0.3", TemplateInfoReaderVersion1_0_0_3.FromJObject },
            { "1.0.0.4", TemplateInfoReaderVersion1_0_0_4.FromJObject },
            { "1.0.0.5", TemplateInfoReaderVersion1_0_0_4.FromJObject }
        };

        private IReadOnlyList<ITemplateParameter> _parameters;

        private IReadOnlyDictionary<string, ICacheTag> _tags;

        private IReadOnlyDictionary<string, ICacheParameter> _cacheParameters;

        public TemplateInfo()
        {
            ShortNameList = new List<string>();
        }

        [JsonIgnore]
        public IReadOnlyList<ITemplateParameter> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    List<ITemplateParameter> parameters = new List<ITemplateParameter>();

                    foreach (KeyValuePair<string, ICacheTag> tagInfo in Tags)
                    {
                        ITemplateParameter param = new TemplateParameter
                        {
                            Name = tagInfo.Key,
                            Documentation = tagInfo.Value.Description,
                            DefaultValue = tagInfo.Value.DefaultValue,
                            Choices = tagInfo.Value.Choices,
                            DataType = "choice"
                        };

                        if (param is IAllowDefaultIfOptionWithoutValue paramWithNoValueDefault
                            && tagInfo.Value is IAllowDefaultIfOptionWithoutValue tagWithNoValueDefault)
                        {
                            paramWithNoValueDefault.DefaultIfOptionWithoutValue = tagWithNoValueDefault.DefaultIfOptionWithoutValue;
                            parameters.Add(paramWithNoValueDefault as TemplateParameter);
                        }
                        else
                        {
                            parameters.Add(param);
                        }
                    }

                    foreach (KeyValuePair<string, ICacheParameter> paramInfo in CacheParameters)
                    {
                        ITemplateParameter param = new TemplateParameter
                        {
                            Name = paramInfo.Key,
                            Documentation = paramInfo.Value.Description,
                            DataType = paramInfo.Value.DataType,
                            DefaultValue = paramInfo.Value.DefaultValue,
                        };

                        if (param is IAllowDefaultIfOptionWithoutValue paramWithNoValueDefault
                            && paramInfo.Value is IAllowDefaultIfOptionWithoutValue infoWithNoValueDefault)
                        {
                            paramWithNoValueDefault.DefaultIfOptionWithoutValue = infoWithNoValueDefault.DefaultIfOptionWithoutValue;
                            parameters.Add(paramWithNoValueDefault as TemplateParameter);
                        }
                        else
                        {
                            parameters.Add(param);
                        }
                    }

                    _parameters = parameters;
                }

                return _parameters;
            }
        }

        [JsonProperty]
        public string MountPointUri { get; set; }

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
        public string ShortName
        {
            get
            {
                if (ShortNameList.Count > 0)
                {
                    return ShortNameList[0];
                }

                return string.Empty;
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

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheTag> Tags
        {
            get
            {
                return _tags;
            }
            set
            {
                _tags = value;
                _parameters = null;
            }
        }

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters
        {
            get
            {
                return _cacheParameters;
            }
            set
            {
                _cacheParameters = value;
                _parameters = null;
            }
        }

        [JsonProperty]
        public string ConfigPlace { get; set; }

        [JsonProperty]
        public string LocaleConfigPlace { get; set; }

        [JsonProperty]
        public string HostConfigPlace { get; set; }

        [JsonProperty]
        public string ThirdPartyNotices { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; set; }

        [JsonIgnore]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        public static TemplateInfo FromJObject(JObject entry, string cacheVersion)
        {
            Func<JObject, TemplateInfo> infoReader;

            if (string.IsNullOrEmpty(cacheVersion) || !_infoVersionReaders.TryGetValue(cacheVersion, out infoReader))
            {
                infoReader = _defaultReader;
            }

            return infoReader(entry);
        }

        // ShortName should get deserialized when it exists, for backwards compat.
        // But moving forward, ShortNameList should be the definitive source.
        // It can still be ShortName in the template.json, but in the caches it'll be ShortNameList
        public bool ShouldSerializeShortName()
        {
            return false;
        }
    }
}
