// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.MSBuildSdkResolver;
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

        public static WorkloadRollbackInfo FromDictionaryForJson(IDictionary<string, string> dictionary, SdkFeatureBand defaultFeatureBand)
        {
            var manifestVersions = dictionary
                .Select(manifest =>
                {
                    ManifestVersion manifestVersion;
                    SdkFeatureBand manifestFeatureBand;
                    var parts = manifest.Value.Split('/');

                    string manifestVersionString = (parts[0]);
                    if (!FXVersion.TryParse(manifestVersionString, out FXVersion version))
                    {
                        throw new FormatException(String.Format(Workload.Install.LocalizableStrings.InvalidVersionForWorkload, manifest.Key, manifestVersionString));
                    }

                    manifestVersion = new ManifestVersion(parts[0]);
                    if (parts.Length == 1)
                    {
                        manifestFeatureBand = defaultFeatureBand;
                    }
                    else
                    {
                        manifestFeatureBand = new SdkFeatureBand(parts[1]);
                    }
                    return (new ManifestId(manifest.Key), manifestVersion, manifestFeatureBand);
                }).ToList();

            return new WorkloadRollbackInfo()
            {
                ManifestVersions = manifestVersions
            };
        }

        public static WorkloadRollbackInfo FromJson(string json, SdkFeatureBand defaultFeatureBand)
        {
            return FromDictionaryForJson(JsonSerializer.Deserialize<IDictionary<string, string>>(json), defaultFeatureBand);
        }

        public Dictionary<string, string> ToDictionaryForJson()
        {
            var dictionary = ManifestVersions.ToDictionary(m => m.Id.ToString(), m => m.Version + "/" + m.FeatureBand, StringComparer.OrdinalIgnoreCase);
            return dictionary;
        }

        public string ToJson()
        {
            var json = JsonSerializer.Serialize(ToDictionaryForJson(), new JsonSerializerOptions() { WriteIndented = true });
            return json;
        }


    }
}
