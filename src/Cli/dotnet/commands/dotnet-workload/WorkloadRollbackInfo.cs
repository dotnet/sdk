// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class WorkloadRollbackInfo
    {
        public IEnumerable<(ManifestId Id, ManifestVersion Version, SdkFeatureBand FeatureBand)> ManifestVersions { get; private set; }

        public static WorkloadRollbackInfo FromManifests(IEnumerable<WorkloadManifestInfo> manifests)
        {
            return new WorkloadRollbackInfo()
            {
                ManifestVersions = manifests.Select(m => (new ManifestId(m.Id), new ManifestVersion(m.Version), new SdkFeatureBand(m.ManifestFeatureBand))).ToList()
            };
        }

        public static WorkloadRollbackInfo FromJson(string json, SdkFeatureBand defaultFeatureBand)
        {
            var manifestVersions = WorkloadSet.FromJson(json, defaultFeatureBand).ManifestVersions
                .Select(kvp => (kvp.Key, kvp.Value.Version, kvp.Value.FeatureBand));

            return new WorkloadRollbackInfo()
            {
                ManifestVersions = manifestVersions
            };
        }

        public Dictionary<string, string> ToDictionaryForJson()
        {
            return ManifestVersions.ToDictionary(m => m.Id.ToString(), m => m.Version + "/" + m.FeatureBand, StringComparer.OrdinalIgnoreCase);
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(ToDictionaryForJson(), new JsonSerializerOptions() { WriteIndented = true });
        }


    }
}
