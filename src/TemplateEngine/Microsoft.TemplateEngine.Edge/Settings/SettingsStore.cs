// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class SettingsStore
    {
        internal SettingsStore()
        {
            ComponentGuidToAssemblyQualifiedName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ComponentTypeToGuidList = new Dictionary<string, HashSet<Guid>>();
            ProbingPaths = new HashSet<string>();
        }

        internal SettingsStore(JObject obj)
            : this()
        {
            JToken componentGuidToAssemblyQualifiedNameToken;
            if (obj.TryGetValue(nameof(ComponentGuidToAssemblyQualifiedName), StringComparison.OrdinalIgnoreCase, out componentGuidToAssemblyQualifiedNameToken))
            {
                JObject? componentGuidToAssemblyQualifiedNameObject = componentGuidToAssemblyQualifiedNameToken as JObject;
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
                JArray? probingPathsArray = probingPathsToken as JArray;
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
                JObject? componentTypeToGuidListObject = componentTypeToGuidListToken as JObject;
                if (componentTypeToGuidListObject != null)
                {
                    foreach (JProperty entry in componentTypeToGuidListObject.Properties())
                    {
                        JArray? values = entry.Value as JArray;

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
        internal Dictionary<string, string> ComponentGuidToAssemblyQualifiedName { get; }

        [JsonProperty]
        internal HashSet<string> ProbingPaths { get; }

        [JsonProperty]
        internal Dictionary<string, HashSet<Guid>> ComponentTypeToGuidList { get; }

        internal static SettingsStore Load(IEngineEnvironmentSettings engineEnvironmentSettings, SettingsFilePaths paths)
        {
            const int MaxLoadAttempts = 20;
            string? userSettings = null;
            using (Timing.Over(engineEnvironmentSettings.Host.Logger, "Read settings"))
            {
                for (int i = 0; i < MaxLoadAttempts; ++i)
                {
                    try
                    {
                        userSettings = paths.ReadAllText(paths.SettingsFile, "{}");
                        break;
                    }
                    catch (IOException)
                    {
                        if (i == MaxLoadAttempts - 1)
                        {
                            throw;
                        }

                        Task.Delay(2).Wait();
                    }
                }
            }

            JObject parsed;
            using (Timing.Over(engineEnvironmentSettings.Host.Logger, "Parse settings"))
            {
                try
                {
                    parsed = JObject.Parse(userSettings);
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
