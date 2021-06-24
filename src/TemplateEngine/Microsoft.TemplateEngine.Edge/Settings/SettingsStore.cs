// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class SettingsStore
    {
        internal SettingsStore(JObject? obj)
        {
            if (obj == null)
            {
                return;
            }

            if (obj.TryGetValue(nameof(ComponentGuidToAssemblyQualifiedName), StringComparison.OrdinalIgnoreCase, out JToken componentGuidToAssemblyQualifiedNameToken))
            {
                if (componentGuidToAssemblyQualifiedNameToken is JObject componentGuidToAssemblyQualifiedNameObject)
                {
                    foreach (JProperty entry in componentGuidToAssemblyQualifiedNameObject.Properties())
                    {
                        if (entry.Value is { Type: JTokenType.String })
                        {
                            ComponentGuidToAssemblyQualifiedName[entry.Name] = entry.Value.ToString();
                        }
                    }
                }
            }

            if (obj.TryGetValue(nameof(ProbingPaths), StringComparison.OrdinalIgnoreCase, out JToken probingPathsToken))
            {
                if (probingPathsToken is JArray probingPathsArray)
                {
                    foreach (JToken path in probingPathsArray)
                    {
                        if (path is { Type: JTokenType.String })
                        {
                            ProbingPaths.Add(path.ToString());
                        }
                    }
                }
            }

            if (obj.TryGetValue(nameof(ComponentTypeToGuidList), StringComparison.OrdinalIgnoreCase, out JToken componentTypeToGuidListToken))
            {
                if (componentTypeToGuidListToken is JObject componentTypeToGuidListObject)
                {
                    foreach (JProperty entry in componentTypeToGuidListObject.Properties())
                    {
                        if (entry.Value is JArray values)
                        {
                            HashSet<Guid> set = new HashSet<Guid>();
                            ComponentTypeToGuidList[entry.Name] = set;

                            foreach (JToken value in values)
                            {
                                if (value is { Type: JTokenType.String })
                                {
                                    if (Guid.TryParse(value.ToString(), out Guid id))
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
        internal Dictionary<string, string> ComponentGuidToAssemblyQualifiedName { get; } = new();

        [JsonProperty]
        internal HashSet<string> ProbingPaths { get; } = new();

        [JsonProperty]
        internal Dictionary<string, HashSet<Guid>> ComponentTypeToGuidList { get; } = new();

        internal static SettingsStore? Load(IEngineEnvironmentSettings engineEnvironmentSettings, SettingsFilePaths paths)
        {
            if (!paths.Exists(paths.SettingsFile))
            {
                return new SettingsStore(null);
            }

            JObject parsed;
            using (Timing.Over(engineEnvironmentSettings.Host.Logger, "Parse settings"))
            {
                try
                {
                    parsed = engineEnvironmentSettings.Host.FileSystem.ReadObject(paths.SettingsFile);
                }
                catch (Exception ex)
                {
                    throw new EngineInitializationException("Error parsing the user settings file", "Settings File", ex);
                }
            }
            SettingsStore settingsStore;
            using (Timing.Over(engineEnvironmentSettings.Host.Logger, "Deserialize user settings"))
            {
                settingsStore = new SettingsStore(parsed);
            }

            using (Timing.Over(engineEnvironmentSettings.Host.Logger, "Init probing paths"))
            {
                if (settingsStore.ProbingPaths.Count == 0)
                {
                    settingsStore.ProbingPaths.Add(paths.Content);
                }
            }
            return settingsStore;
        }
    }
}
