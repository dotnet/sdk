// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;
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

                TemplateInfo info = new TemplateInfo(identity, name, shortNames, mountPointUri, configPlace)
                {
                    Author = entry.ToString(nameof(Author))
                };
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
                        var defaultOverrides = item.Value.ToStringDictionary(propertyName: nameof(IBaselineInfo.DefaultOverrides));
                        if (defaultOverrides is null)
                        {
                            continue;
                        }

                        IBaselineInfo baseline = new BaselineInfo(defaultOverrides, item.Value.ToString(nameof(IBaselineInfo.Description)));
                        baselineInfo.Add(item.Name, baseline);
                    }
                    info.BaselineInfo = baselineInfo;
                }

                //read parameters
#pragma warning disable CS0618 // Type or member is obsolete
                JArray? parametersArray = entry.Get<JArray>(nameof(Parameters));
#pragma warning restore CS0618 // Type or member is obsolete
                if (parametersArray != null)
                {
                    List<ITemplateParameter> templateParameters = new List<ITemplateParameter>();
                    foreach (JToken item in parametersArray)
                    {
                        if (item is JObject jobj)
                        {
                            templateParameters.Add(new TemplateParameter(jobj));
                        }
                    }
                    info.ParameterDefinitions = new ParameterDefinitionSet(templateParameters);
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

                //read parameters
                JArray? constraintsArray = entry.Get<JArray>(nameof(info.Constraints));
                if (constraintsArray != null)
                {
                    List<TemplateConstraintInfo> constraints = new List<TemplateConstraintInfo>();
                    foreach (JToken item in constraintsArray)
                    {
                        string? type = item.ToString(nameof(TemplateConstraintInfo.Type));
                        if (string.IsNullOrWhiteSpace(type))
                        {
                            throw new ArgumentException($"{nameof(entry)} has {nameof(info.Constraints)} property which item doesn't have {nameof(TemplateConstraintInfo.Type)}.", nameof(entry));
                        }
                        constraints.Add(new TemplateConstraintInfo(type!, item.ToString(nameof(TemplateConstraintInfo.Args))));
                    }
                    info.Constraints = constraints;
                }

                return info;
            }
        }
    }
}
