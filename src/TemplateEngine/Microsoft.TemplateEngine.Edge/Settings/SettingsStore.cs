// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class SettingsStore
    {
        internal SettingsStore(JsonObject? obj)
        {
            if (obj == null)
            {
                return;
            }

            if (obj.TryGetValueCaseInsensitive(nameof(ComponentGuidToAssemblyQualifiedName), out JsonNode? componentGuidToAssemblyQualifiedNameToken))
            {
                if (componentGuidToAssemblyQualifiedNameToken is JsonObject componentGuidToAssemblyQualifiedNameObject)
                {
                    foreach (var entry in componentGuidToAssemblyQualifiedNameObject)
                    {
                        if (entry.Value?.GetValueKind() == JsonValueKind.String)
                        {
                            ComponentGuidToAssemblyQualifiedName[entry.Key] = entry.Value.GetValue<string>();
                        }
                    }
                }
            }

            if (obj.TryGetValueCaseInsensitive(nameof(ProbingPaths), out JsonNode? probingPathsToken))
            {
                if (probingPathsToken is JsonArray probingPathsArray)
                {
                    foreach (JsonNode? path in probingPathsArray)
                    {
                        if (path?.GetValueKind() == JsonValueKind.String)
                        {
                            ProbingPaths.Add(path.GetValue<string>());
                        }
                    }
                }
            }

            if (obj.TryGetValueCaseInsensitive(nameof(ComponentTypeToGuidList), out JsonNode? componentTypeToGuidListToken))
            {
                if (componentTypeToGuidListToken is JsonObject componentTypeToGuidListObject)
                {
                    foreach (var entry in componentTypeToGuidListObject)
                    {
                        if (entry.Value is JsonArray values)
                        {
                            HashSet<Guid> set = new HashSet<Guid>();
                            ComponentTypeToGuidList[entry.Key] = set;

                            foreach (JsonNode? value in values)
                            {
                                if (value?.GetValueKind() == JsonValueKind.String)
                                {
                                    if (Guid.TryParse(value.GetValue<string>(), out Guid id))
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

        [JsonInclude]
        internal Dictionary<string, string> ComponentGuidToAssemblyQualifiedName { get; } = new();

        [JsonInclude]
        internal HashSet<string> ProbingPaths { get; } = new();

        [JsonInclude]
        internal Dictionary<string, HashSet<Guid>> ComponentTypeToGuidList { get; } = new();

        internal static SettingsStore Load(IEngineEnvironmentSettings engineEnvironmentSettings, SettingsFilePaths paths)
        {
            if (!paths.Exists(paths.SettingsFile))
            {
                return new SettingsStore(null);
            }

            JsonObject parsed;
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
