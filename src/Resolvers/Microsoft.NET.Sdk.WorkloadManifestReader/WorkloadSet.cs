﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.MSBuildSdkResolver;
using Strings = Microsoft.NET.Sdk.Localization.Strings;

using System.Text.Json;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadSet
    {
        public Dictionary<ManifestId, (ManifestVersion Version, SdkFeatureBand FeatureBand)> ManifestVersions = new();

        //  TODO: Generate version from hash of manifest versions if not otherwise set
        public string? Version { get; set; }

        //  Indicates that a workload set is a baseline workload set that was installed with the .NET SDK.
        //  It should not be subject to normal garbage collection unless the SDK that installed it is removed
        public bool IsBaselineWorkloadSet { get; set; }

        public static WorkloadSet FromManifests(IEnumerable<WorkloadManifestInfo> manifests)
        {
            return new WorkloadSet()
            {
                ManifestVersions = manifests.ToDictionary(m => new ManifestId(m.Id), m => (new ManifestVersion(m.Version), new SdkFeatureBand(m.ManifestFeatureBand)))
            };
        }

        public static WorkloadSet FromDictionaryForJson(IDictionary<string, string?> dictionary, SdkFeatureBand defaultFeatureBand)
        {
            var manifestVersions = dictionary
                .Select(manifest =>
                {
                    ManifestVersion manifestVersion;
                    SdkFeatureBand manifestFeatureBand;
                    var parts = manifest.Value?.Split('/');

                    string manifestVersionString = string.Empty;
                    if (parts != null)
                    {
                        manifestVersionString = parts[0];
                    }
                    if (!FXVersion.TryParse(manifestVersionString, out FXVersion? version))
                    {
                        throw new FormatException(string.Format(Strings.InvalidVersionForWorkload, manifest.Key, manifestVersionString));
                    }

                    manifestVersion = new ManifestVersion(parts?[0]);
                    if (parts != null && parts.Length == 1)
                    {
                        manifestFeatureBand = defaultFeatureBand;
                    }
                    else
                    {
                        manifestFeatureBand = new SdkFeatureBand(parts?[1]);
                    }
                    return (id: new ManifestId(manifest.Key), manifestVersion, manifestFeatureBand);
                }).ToDictionary(t => t.id, t => (t.manifestVersion, t.manifestFeatureBand));

            return new WorkloadSet()
            {
                ManifestVersions = manifestVersions
            };
        }

        public static WorkloadSet FromJson(string json, SdkFeatureBand defaultFeatureBand)
        {
            var jsonSerializerOptions = new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            return FromDictionaryForJson(JsonSerializer.Deserialize<IDictionary<string, string>>(json, jsonSerializerOptions)!, defaultFeatureBand);
        }

        public Dictionary<string, string> ToDictionaryForJson()
        {
            var dictionary = ManifestVersions.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Version + "/" + kvp.Value.FeatureBand, StringComparer.OrdinalIgnoreCase);
            return dictionary;
        }

        public string ToJson()
        {
            var json = JsonSerializer.Serialize(ToDictionaryForJson(), new JsonSerializerOptions() { WriteIndented = true });
            return json;
        }

    }
}
