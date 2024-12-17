// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.MSBuildSdkResolver;
using Strings = Microsoft.NET.Sdk.Localization.Strings;

using System.Text.Json;
using Microsoft.DotNet.Workloads.Workload;

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

        public static WorkloadSet FromWorkloadSetFolder(string path, string workloadSetVersion, SdkFeatureBand defaultFeatureBand)
        {
            WorkloadSet? workloadSet = null;
            foreach (var jsonFile in Directory.GetFiles(path, "*.workloadset.json"))
            {
                var newWorkloadSet = WorkloadSet.FromJson(File.ReadAllText(jsonFile), defaultFeatureBand);
                if (workloadSet == null)
                {
                    workloadSet = newWorkloadSet;
                }
                else
                {
                    //  If there are multiple workloadset.json files, merge them
                    foreach (var kvp in newWorkloadSet.ManifestVersions)
                    {
                        if (workloadSet.ManifestVersions.ContainsKey(kvp.Key))
                        {
                            throw new InvalidOperationException($"Workload set files in {path} defined the same manifest ({kvp.Key}) multiple times");
                        }
                        workloadSet.ManifestVersions.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            if (workloadSet == null)
            {
                throw new InvalidOperationException("No workload set information found in: " + path);
            }

            if (File.Exists(Path.Combine(path, "baseline.workloadset.json")))
            {
                workloadSet.IsBaselineWorkloadSet = true;
            }

            workloadSet.Version = workloadSetVersion;

            return workloadSet;
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

        //  Corresponding method for opposite direction is in WorkloadManifestUpdater, as its implementation depends on NuGetVersion,
        //  which we'd like to avoid adding as a dependency here.
        public static string WorkloadSetVersionToWorkloadSetPackageVersion(string setVersion, out SdkFeatureBand sdkFeatureBand)
        {
            string[] sections = setVersion.Split(new char[] { '-', '+' }, 2);
            string versionCore = sections[0];
            string? preReleaseOrBuild = sections.Length > 1 ? sections[1] : null;

            string[] coreComponents = versionCore.Split('.');
            string major = coreComponents[0];
            string minor = coreComponents[1];
            string patch = coreComponents[2];

            string packageVersion = $"{major}.{patch}.";
            if (coreComponents.Length == 3)
            {
                //  No workload set patch version
                packageVersion += "0";

                //  Use preview specifier (if any) from workload set version as part of SDK feature band
                sdkFeatureBand = new SdkFeatureBand(setVersion);
            }
            else
            {
                //  Workload set version has workload patch version (ie 4 components)
                packageVersion += coreComponents[3];

                //  Don't include any preview specifiers in SDK feature band
                sdkFeatureBand = new SdkFeatureBand($"{major}.{minor}.{patch}");
            }

            if (preReleaseOrBuild != null)
            {
                //  Figure out if we split on a '-' or '+'
                char separator = setVersion[sections[0].Length];
                packageVersion += separator + preReleaseOrBuild;
            }

            return packageVersion;
        }

        public static SdkFeatureBand GetWorkloadSetFeatureBand(string setVersion)
        {
            WorkloadSetVersionToWorkloadSetPackageVersion(setVersion, out SdkFeatureBand sdkFeatureBand);
            return sdkFeatureBand;
        }
    }
}
