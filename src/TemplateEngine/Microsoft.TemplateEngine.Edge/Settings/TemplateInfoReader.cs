// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal partial class TemplateInfo
    {
        internal class TemplateInfoReader
        {
            internal static TemplateInfo FromJObject(JObject entry)
            {
                string identity = entry.ToString(nameof(Identity)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Identity)} property.", nameof(entry));
                string name = entry.ToString(nameof(Name)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Name)} property.", nameof(entry));
                string mountPointUri = entry.ToString(nameof(MountPointUri)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(MountPointUri)} property.", nameof(entry));
                string configPlace = entry.ToString(nameof(ConfigPlace)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(ConfigPlace)} property.", nameof(entry));
                JToken? shortNameToken = entry.Get<JToken>(nameof(ShortNameList));
                IEnumerable<string> shortNames = shortNameToken.JTokenStringOrArrayToCollection(Array.Empty<string>());

                TemplateInfo info = new TemplateInfo(identity, name, shortNames, mountPointUri, configPlace);

                info.Author = entry.ToString(nameof(Author));
                JArray? classificationsArray = entry.Get<JArray>(nameof(Classifications));
                if (classificationsArray != null)
                {
                    List<string> classifications = new List<string>();
                    foreach (JToken item in classificationsArray)
                    {
                        classifications.Add(item.ToString());
                    }
                    info.Classifications = classifications;
                }

                info.DefaultName = entry.ToString(nameof(DefaultName));
                info.Description = entry.ToString(nameof(Description));
                info.GeneratorId = Guid.Parse(entry.ToString(nameof(GeneratorId)));
                info.GroupIdentity = entry.ToString(nameof(GroupIdentity));
                info.Precedence = entry.ToInt32(nameof(Precedence));

                info.LocaleConfigPlace = entry.ToString(nameof(LocaleConfigPlace));
                info.HostConfigPlace = entry.ToString(nameof(HostConfigPlace));
                info.ThirdPartyNotices = entry.ToString(nameof(ThirdPartyNotices));

                JObject? baselineJObject = entry.Get<JObject>(nameof(ITemplateInfo.BaselineInfo));
                Dictionary<string, IBaselineInfo> baselineInfo = new Dictionary<string, IBaselineInfo>();
                if (baselineJObject != null)
                {
                    foreach (JProperty item in baselineJObject.Properties())
                    {
                        IBaselineInfo baseline = new BaselineCacheInfo()
                        {
                            Description = item.Value.ToString(nameof(IBaselineInfo.Description)),
                            DefaultOverrides = item.Value.ToStringDictionary(propertyName: nameof(IBaselineInfo.DefaultOverrides))
                        };
                        baselineInfo.Add(item.Name, baseline);
                    }
                    info.BaselineInfo = baselineInfo;
                }

                //read parameters
                JArray? parametersArray = entry.Get<JArray>(nameof(Parameters));
                if (parametersArray != null)
                {
                    List<ITemplateParameter> templateParameters = new List<ITemplateParameter>();
                    foreach (JObject item in parametersArray)
                    {
                        templateParameters.Add(new TemplateParameter(item));
                    }
                    info.Parameters = templateParameters;
                }

                //read tags
                // tags are just "name": "description"
                // e.g.: "language": "C#"
                JObject? tagsObject = entry.Get<JObject>(nameof(TagsCollection));
                if (tagsObject != null)
                {
                    Dictionary<string, string> tags = new Dictionary<string, string>();
                    foreach (JProperty item in tagsObject.Properties())
                    {
                        tags.Add(item.Name.ToString(), item.Value.ToString());
                    }
                    info.TagsCollection = tags;
                }

                info.HostData = entry.Get<JObject>(nameof(info.HostData));
                JArray? postActionsArray = entry.Get<JArray>(nameof(info.PostActions));
                if (postActionsArray != null)
                {
                    List<Guid> postActions = new List<Guid>();
                    foreach (JToken item in postActionsArray)
                    {
                        if (Guid.TryParse(item.ToString(), out Guid id))
                        {
                            postActions.Add(id);
                        }
                    }
                    info.PostActions = postActions;
                }
                return info;
            }
        }
    }
}
