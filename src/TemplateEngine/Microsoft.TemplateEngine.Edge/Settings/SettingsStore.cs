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
            MountPoints = new List<MountPointInfo>();
            ComponentGuidToAssemblyQualifiedName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ComponentTypeToGuidList = new Dictionary<string, List<Guid>>();
            ProbingPaths = new HashSet<string>();
        }

        public SettingsStore(JObject obj)
            : this()
        {
            JToken mountPointsToken;
            if (obj.TryGetValue("MountPoints", StringComparison.OrdinalIgnoreCase, out mountPointsToken))
            {
                JArray mountPointsArray = mountPointsToken as JArray;
                if (mountPointsArray != null)
                {
                    foreach (JToken entry in mountPointsArray)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            Guid parentMountPointId;
                            Guid mountPointFactoryId;
                            Guid mountPointId;
                            string place;

                            JObject mp = (JObject) entry;
                            JToken parentMountPointIdToken;
                            if (!mp.TryGetValue("ParentMountPointId", StringComparison.OrdinalIgnoreCase, out parentMountPointIdToken) || parentMountPointIdToken == null || parentMountPointIdToken.Type != JTokenType.String || !Guid.TryParse(parentMountPointIdToken.ToString(), out parentMountPointId))
                            {
                                continue;
                            }

                            JToken mountPointFactoryIdToken;
                            if (!mp.TryGetValue("MountPointFactoryId", StringComparison.OrdinalIgnoreCase, out mountPointFactoryIdToken) || mountPointFactoryIdToken == null || mountPointFactoryIdToken.Type != JTokenType.String || !Guid.TryParse(mountPointFactoryIdToken.ToString(), out mountPointFactoryId))
                            {
                                continue;
                            }

                            JToken mountPointIdToken;
                            if (!mp.TryGetValue("MountPointId", StringComparison.OrdinalIgnoreCase, out mountPointIdToken) || mountPointIdToken == null || mountPointIdToken.Type != JTokenType.String || !Guid.TryParse(mountPointIdToken.ToString(), out mountPointId))
                            {
                                continue;
                            }

                            JToken placeToken;
                            if (!mp.TryGetValue("Place", StringComparison.OrdinalIgnoreCase, out placeToken) || placeToken == null || placeToken.Type != JTokenType.String)
                            {
                                continue;
                            }

                            place = placeToken.ToString();
                            MountPointInfo mountPoint = new MountPointInfo(parentMountPointId, mountPointFactoryId, mountPointId, place);
                            MountPoints.Add(mountPoint);
                        }
                    }
                }
            }

            JToken componentGuidToAssemblyQualifiedNameToken;
            if (obj.TryGetValue("ComponentGuidToAssemblyQualifiedName", StringComparison.OrdinalIgnoreCase, out componentGuidToAssemblyQualifiedNameToken))
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
            if (obj.TryGetValue("ProbingPaths", StringComparison.OrdinalIgnoreCase, out probingPathsToken))
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
            if (obj.TryGetValue("ComponentTypeToGuidList", StringComparison.OrdinalIgnoreCase, out componentTypeToGuidListToken))
            {
                JObject componentTypeToGuidListObject = componentTypeToGuidListToken as JObject;
                if (componentTypeToGuidListObject != null)
                {
                    foreach (JProperty entry in componentTypeToGuidListObject.Properties())
                    {
                        JArray values = entry.Value as JArray;

                        if (values != null)
                        {
                            List<Guid> set = new List<Guid>();
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
        public List<MountPointInfo> MountPoints { get; }

        [JsonProperty]
        public Dictionary<string, string> ComponentGuidToAssemblyQualifiedName { get; }

        [JsonProperty]
        public HashSet<string> ProbingPaths { get; }

        [JsonProperty]
        public Dictionary<string, List<Guid>> ComponentTypeToGuidList { get; }
    }
}
