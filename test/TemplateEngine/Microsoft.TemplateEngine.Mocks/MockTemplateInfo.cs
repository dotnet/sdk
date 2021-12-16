// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

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
        private string? _identity;
        private string? _name;

        private Dictionary<string, TemplateParameter> _parameters = new Dictionary<string, TemplateParameter>();

        private string[] _baselineInfo = Array.Empty<string>();

        private string[] _classifications = Array.Empty<string>();

        private string[] _shortNameList = Array.Empty<string>();

        private Guid[] _postActions = Array.Empty<Guid>();

        private Dictionary<string, string> _tags = new Dictionary<string, string>();

        public MockTemplateInfo()
        {
        }

        public MockTemplateInfo(string shortName, string? name = null, string?identity = null, string? groupIdentity = null, int precedence = 0, string? author = null)
            : this(new string[] { shortName }, name, identity, groupIdentity, precedence, author)
        {
        }

        public MockTemplateInfo(string[] shortNames, string? name = null, string? identity = null, string? groupIdentity = null, int precedence = 0, string? author = null) : this()
        {
            _shortNameList = shortNames;
            if (string.IsNullOrEmpty(name))
            {
                _name = "Template " + shortNames[0];
            }
            else
            {
                _name = name;
            }

            if (string.IsNullOrEmpty(identity))
            {
                _identity = shortNames[0];
            }
            else
            {
                _identity = identity;
            }

            Precedence = precedence;
            GroupIdentity = groupIdentity;
            Author = author;
        }

        public string? Author { get; private set; }

        public string? Description { get; private set; }

        public IReadOnlyList<string> Classifications
        {
            get
            {
                return _classifications;
            }
        }

        public string? DefaultName { get; }

        public string Identity => _identity ?? throw new Exception($"{nameof(_identity)} was not initialized.");

        public Guid GeneratorId { get; }

        public string? GroupIdentity { get; private set; }

        public int Precedence { get; private set; }

        public string Name => _name ?? throw new Exception($"{nameof(_name)} was not initialized.");

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

        [Obsolete("Use Parameters instead.")]
        IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags => throw new NotImplementedException();

        [Obsolete("Use Parameters instead.")]
        IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters => throw new NotImplementedException();

        public IReadOnlyList<ITemplateParameter> Parameters
        {
            get
            {
                List<ITemplateParameter> parameters = new List<ITemplateParameter>();
                foreach (var param in _parameters)
                {
                    parameters.Add(param.Value);
                }
                return parameters;
            }            
        }

        public string MountPointUri => "FakeMountPoint";

        public string ConfigPlace => "FakeConfigPlace";

        public string? LocaleConfigPlace { get; }

        public string? HostConfigPlace { get; }

        public string? ThirdPartyNotices { get; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo
        {
            get
            {
                return _baselineInfo.ToDictionary(k => k, k => (IBaselineInfo)new BaselineCacheInfo());
            }
        }

        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        public DateTime? ConfigTimestampUtc { get; }

        public IReadOnlyDictionary<string, string> TagsCollection => _tags;

        public IReadOnlyList<Guid> PostActions => _postActions;

        public MockTemplateInfo WithParameters(params string[] parameters)
        {
            foreach (var param in parameters)
            {
                _parameters[param] = new TemplateParameter(param, "parameter", "string", priority: TemplateParameterPriority.Optional);
            }
            return this;
        }

        public MockTemplateInfo WithTag(string tagName,  string value)
        {
            _tags.Add(tagName, value);
            return this;
        }

        public MockTemplateInfo WithChoiceParameter(string name, params string[] values)
        {
            _parameters.Add(name, new TemplateParameter(
                name,
                type: "parameter",
                datatype: "choice",
                priority: TemplateParameterPriority.Optional,
                choices: values.ToDictionary(v => v, v => new ParameterChoice(null, null))));
            return this;
        }

        public MockTemplateInfo WithChoiceParameter(string name, string[] values, bool isRequired = false, string? defaultValue = null, string? defaultIfNoOptionValue = null, string? description = null)
        {
            _parameters.Add(name, new TemplateParameter(
                name,
                type: "parameter",
                datatype: "choice",
                description: description,
                priority: isRequired ? TemplateParameterPriority.Required : TemplateParameterPriority.Optional,
                defaultValue: defaultValue,
                defaultIfOptionWithoutValue: defaultIfNoOptionValue,
                choices: values.ToDictionary(v => v, v => new ParameterChoice(null, null))));
            return this;
        }

        public MockTemplateInfo WithDescription(string? description)
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

        public MockTemplateInfo WithParameter(string paramName, string paramType = "string", bool isRequired = false, string? defaultValue = null, string? defaultIfNoOptionValue = null, string? description = null)
        {
            _parameters[paramName] = new TemplateParameter(
                paramName,
                "parameter",
                paramType,
                isRequired ? TemplateParameterPriority.Required : TemplateParameterPriority.Optional,
                description: description,
                defaultValue: defaultValue,
                defaultIfOptionWithoutValue: defaultIfNoOptionValue);
            return this;
        }
        
        public MockTemplateInfo WithPostActions(params Guid[] postActions)
        {
            if (_postActions.Length == 0)
            {
                _postActions = postActions;
            }
            else
            {
                _postActions = _postActions.Concat(postActions).ToArray();
            }
            return this;
        }

        #region XUnitSerializable implementation

        public void Deserialize(IXunitSerializationInfo info)
        {
            _name = info.GetValue<string>("template_name");
            Precedence = info.GetValue<int>("template_precedence");
            _identity = info.GetValue<string>("template_identity");
            GroupIdentity = info.GetValue<string>("template_group");
            Description = info.GetValue<string>("template_description");
            Author = info.GetValue<string>("template_author");
            _tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(info.GetValue<string>("template_tags"))
                ?? throw new Exception("Deserialiation failed");
            _parameters = JsonConvert.DeserializeObject<Dictionary<string, TemplateParameter>>(info.GetValue<string>("template_params"))
                         ?? throw new Exception("Deserialiation failed");
            _baselineInfo = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_baseline"))
                         ?? throw new Exception("Deserialiation failed");
            _classifications = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_classifications"))
                         ?? throw new Exception("Deserialiation failed");
            _shortNameList = JsonConvert.DeserializeObject<string[]>(info.GetValue<string>("template_shortname"))
                         ?? throw new Exception("Deserialiation failed");
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

            info.AddValue("template_tags", JsonConvert.SerializeObject(_tags), typeof(string));
            info.AddValue("template_params", JsonConvert.SerializeObject(_parameters), typeof(string));
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
                _ = sb.Append("Group:" + GroupIdentity + ";");
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
            if (_parameters.Any())
            {
                _ = sb.Append("Parameters:" + string.Join(",", _parameters) + ";");
            }
            if (_baselineInfo.Any())
            {
                _ = sb.Append("Baseline:" + string.Join(",", _baselineInfo) + ";");
            }
            if (_tags.Any())
            {
                _ = sb.Append("Tags:" + string.Join(",", _tags.Select(t => t.Key + "(" + t.Value + ")")) + ";");
            }
            return sb.ToString();
        }

        #endregion XUnitSerializable implementation

        private class TemplateParameter : ITemplateParameter
        {
            [JsonConstructor]
            internal TemplateParameter(
                string name,
                string type,
                string datatype,
                TemplateParameterPriority priority = default,
                bool isName = false,
                string? defaultValue = null,
                string? defaultIfOptionWithoutValue = null,
                string? description = null,
                string? displayName = null,
                IReadOnlyDictionary<string, ParameterChoice>? choices = null)
            {
                Name = name;
                Type = type;
                DataType = datatype;
                Priority = priority;
                IsName = isName;
                DefaultValue = defaultValue;
                DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
                Description = description;
                DisplayName = displayName;

                if (this.IsChoice())
                {
                    Choices = choices ?? new Dictionary<string, ParameterChoice>();
                }
            }

            [Obsolete("Use Description instead.")]
            public string? Documentation => Description;

            [JsonProperty]
            public string Name { get; }

            [JsonProperty]
            public TemplateParameterPriority Priority { get; }

            [JsonProperty]
            public string Type { get; }

            [JsonProperty]
            public bool IsName { get; }

            [JsonProperty]
            public string? DefaultValue { get; }

            [JsonProperty]
            public string DataType { get; set; }

            [JsonProperty]
            public string? DefaultIfOptionWithoutValue { get; set; }

            [JsonProperty]
            public IReadOnlyDictionary<string, ParameterChoice>? Choices { get; }

            [JsonProperty]
            public string? Description { get; }

            [JsonProperty]
            public string? DisplayName { get; }
        }
    }

}
