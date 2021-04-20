// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class SettingsStore
    {
        public SettingsStore()
        {
            ComponentGuidToAssemblyQualifiedName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ComponentTypeToGuidList = new Dictionary<string, HashSet<Guid>>();
            ProbingPaths = new HashSet<string>();
        }

        public SettingsStore(JObject obj)
            : this()
        {
            JToken componentGuidToAssemblyQualifiedNameToken;
            if (obj.TryGetValue(nameof(ComponentGuidToAssemblyQualifiedName), StringComparison.OrdinalIgnoreCase, out componentGuidToAssemblyQualifiedNameToken))
            {
                JObject componentGuidToAssemblyQualifiedNameObject = componentGuidToAssemblyQualifiedNameToken as JObject;
                if (componentGuidToAssemblyQualifiedNameObject != null)
                {
                    foreach (JProperty entry in componentGuidToAssemblyQualifiedNameObject.Properties())
                    {
                        if (entry.Value != null && entry.Value.Type == JTokenType.String)
                        {
                            ComponentGuidToAssemblyQualifiedName[entry.Name] = entry.Value.ToString();
                        }
                    }
                }
            }

            JToken probingPathsToken;
            if (obj.TryGetValue(nameof(ProbingPaths), StringComparison.OrdinalIgnoreCase, out probingPathsToken))
            {
                JArray probingPathsArray = probingPathsToken as JArray;
                if (probingPathsArray != null)
                {
                    foreach (JToken path in probingPathsArray)
                    {
                        if (path != null && path.Type == JTokenType.String)
                        {
                            ProbingPaths.Add(path.ToString());
                        }
                    }
                }
            }

            JToken componentTypeToGuidListToken;
            if (obj.TryGetValue(nameof(ComponentTypeToGuidList), StringComparison.OrdinalIgnoreCase, out componentTypeToGuidListToken))
            {
                JObject componentTypeToGuidListObject = componentTypeToGuidListToken as JObject;
                if (componentTypeToGuidListObject != null)
                {
                    foreach (JProperty entry in componentTypeToGuidListObject.Properties())
                    {
                        JArray values = entry.Value as JArray;

                        if (values != null)
                        {
                            HashSet<Guid> set = new HashSet<Guid>();
                            ComponentTypeToGuidList[entry.Name] = set;

                            foreach (JToken value in values)
                            {
                                if (value != null && value.Type == JTokenType.String)
                                {
                                    Guid id;
                                    if (Guid.TryParse(value.ToString(), out id))
                                    {
                                        set.Add(id);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [JsonProperty]
        public Dictionary<string, string> ComponentGuidToAssemblyQualifiedName { get; }

        [JsonProperty]
        public HashSet<string> ProbingPaths { get; }

        [JsonProperty]
        public Dictionary<string, HashSet<Guid>> ComponentTypeToGuidList { get; }
    }
}
