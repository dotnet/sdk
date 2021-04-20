// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockTemplateInfo : ITemplateInfo, IXunitSerializable
    {
        public MockTemplateInfo()
        {
        }

        public MockTemplateInfo(string shortName, string name = null, string identity = null, string groupIdentity = null, int precedence = 0, string author = null)
            : this(new string[] { shortName }, name, identity, groupIdentity, precedence, author)
        {
        }

        public MockTemplateInfo(string[] shortNames, string name = null, string identity = null, string groupIdentity = null, int precedence = 0, string author = null) : this()
        {
            _shortNameList = shortNames;
            if (string.IsNullOrEmpty(name))
            {
                Name = "Template " + shortNames[0];
            }
            else
            {
                Name = name;
            }

            if (string.IsNullOrEmpty(identity))
            {
                Identity = shortNames[0];
            }
            else
            {
                Identity = identity;
            }

            Precedence = precedence;
            GroupIdentity = groupIdentity;
            Identity = identity;
            Author = author;
        }

        public MockTemplateInfo WithParameters(params string[] parameters)
        {
            if (_cacheParameters.Length == 0)
            {
                _cacheParameters = parameters;
            }
            else
            {
                _cacheParameters = _cacheParameters.Concat(parameters).ToArray();
            }
            return this;
        }
        public MockTemplateInfo WithTag(string tagName, params string[] values)
        {
            _tags.Add(tagName, values);
            return this;
        }

        public MockTemplateInfo WithDescription(string description)
        {
            Description = description;
            return this;
        }

        public MockTemplateInfo WithBaselineInfo(params string[] baseline)
        {
            if (_baselineInfo.Length == 0)
            {
                _baselineInfo = baseline;
            }
            else
            {
                _baselineInfo = _baselineInfo.Concat(baseline).ToArray();
            }
            return this;
        }

        public MockTemplateInfo WithClassifications(params string[] classifications)
        {
            if (_classifications.Length == 0)
            {
                _classifications = classifications;
            }
            else
            {
                _classifications = _classifications.Concat(classifications).ToArray();
            }
            return this;
        }

        private string[] _cacheParameters = new string[0];
        private string[] _baselineInfo = new string[0];
        private string[] _classifications = new string[0];
        private string[] _shortNameList = new string[0];
        private Dictionary<string, string[]> _tags = new Dictionary<string, string[]>();

        public string Author { get; private set; }

        public string Description { get; private set; }

        public IReadOnlyList<string> Classifications
        {
            get
            {
                return _classifications;
            }
        }

        public string DefaultName { get; }

        public string Identity { get; private set; }

        public Guid GeneratorId { get; }

        public string GroupIdentity { get; private set; }

        public int Precedence { get; private set; }

        public string Name { get; private set; }

        public string ShortName
        {
            get
            {
                if (_shortNameList.Length > 0)
                {
                    return _shortNameList[0];
                }
                return string.Empty;
            }
        }

        public IReadOnlyList<string> ShortNameList => _shortNameList;

        public IReadOnlyDictionary<string, ICacheTag> Tags
        {
            get
            {
                return _tags.ToDictionary(kvp => kvp.Key, kvp => CreateTestCacheTag(kvp.Value));
            }
        }

        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters
        {
            get
            {
                return _cacheParameters.ToDictionary(param => param, kvp => (ICacheParameter)new CacheParameter());
            }
        }

        private IReadOnlyList<ITemplateParameter> _parameters;
        public IReadOnlyList<ITemplateParameter> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    List<ITemplateParameter> parameters = new List<ITemplateParameter>();
                    PopulateParametersFromTags(parameters);
                    PopulateParametersFromCacheParameters(parameters);
                    _parameters = parameters;
                }
                return _parameters;
            }
            set
            {
                _parameters = value;
            }
        }

        public string MountPointUri { get; }

        public string ConfigPlace { get; }

        public string LocaleConfigPlace { get; }

        public string HostConfigPlace { get; }

        public string ThirdPartyNotices { get; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo
        {
            get
            {
                return _baselineInfo.ToDictionary(k => k, k => (IBaselineInfo) new BaselineCacheInfo());
            }
        }

        public bool HasScriptRunningPostActions { get; set; }

        public DateTime? ConfigTimestampUtc { get; }

        private static ICacheTag CreateTestCacheTag(IReadOnlyList<string> choiceList, string tagDescription = null, string defaultValue = null, string defaultIfOptionWithoutValue = null)
        {
            Dictionary<string, ParameterChoice> choicesDict = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
            foreach (string choice in choiceList)
            {
                choicesDict.Add(choice, new ParameterChoice(string.Empty, string.Empty));
            }
            return new CacheTag(string.Empty, tagDescription, choicesDict, defaultValue, defaultIfOptionWithoutValue);
        }

        private void PopulateParametersFromTags (List<ITemplateParameter> parameters)
        {
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
        }

        private void PopulateParametersFromCacheParameters(List<ITemplateParameter> parameters)
        {
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
        }

        #region XUnitSerializable implementation
        public void Deserialize(IXunitSerializationInfo info)
        {
            Name = info.GetValue<string>("template_name");
            Precedence = info.GetValue<int>("template_precedence");
            Identity = info.GetValue<string>("template_identity");
            GroupIdentity = info.GetValue<string>("template_group");
            Description = info.GetValue<string>("template_description");
            Author = info.GetValue<string>("template_author");
            HasScriptRunningPostActions = info.GetValue<bool>("template_hasPostActions");

            _tags = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(info.GetValue<string>("template_tags"));
            _cacheParameters = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_params"));
            _baselineInfo = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_baseline"));
            _classifications = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_classifications"));
            _shortNameList = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_shortname"));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("template_name", Name, typeof(string));
            info.AddValue("template_shortname", JsonConvert.SerializeObject(_shortNameList), typeof(string));
            info.AddValue("template_precedence", Precedence, typeof(int));
            info.AddValue("template_identity", Identity, typeof(string));
            info.AddValue("template_group", GroupIdentity, typeof(string));
            info.AddValue("template_description", Description, typeof(string));
            info.AddValue("template_author", Author, typeof(string));
            info.AddValue("template_hasPostActions", HasScriptRunningPostActions, typeof(bool));

            info.AddValue("template_tags", JsonConvert.SerializeObject(_tags), typeof(string));
            info.AddValue("template_params", JsonConvert.SerializeObject(_cacheParameters), typeof(string));
            info.AddValue("template_baseline", JsonConvert.SerializeObject(_baselineInfo), typeof(string));
            info.AddValue("template_classifications", JsonConvert.SerializeObject(_classifications), typeof(string));
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            _ = sb.Append("Short name:" + string.Join(",", _shortNameList) + ";");
            _ = sb.Append("Identity:" + Identity + ";");

            if (string.IsNullOrEmpty(GroupIdentity))
            {
                _ = sb.Append("Group:<not set>;");
            }
            else
            {
                _ = sb.Append("Group:" + GroupIdentity +";");
            }
            if (Precedence != 0)
            {
                _ = sb.Append("Precedence:" + Precedence + ";");
            }
            if (!string.IsNullOrEmpty(Author))
            {
                _ = sb.Append("Author:" + Author + ";");
            }
            if (Classifications.Any())
            {
                _ = sb.Append("Classifications:" + string.Join(",", _classifications) + ";");
            }
            if (_cacheParameters.Any())
            {
                _ = sb.Append("Parameters:" + string.Join(",", _cacheParameters) + ";");
            }
            if (_baselineInfo.Any())
            {
                _ = sb.Append("Baseline:" + string.Join(",", _baselineInfo) + ";");
            }
            if (_tags.Any())
            {
                _ = sb.Append("Tags:" + string.Join(",", _tags.Select(t => t.Key + "(" + string.Join("|",t.Value) + ")")) + ";");
            }

            return sb.ToString();

        }
        #endregion
    }
}
