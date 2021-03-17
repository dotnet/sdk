// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// An SDK workload manifest
    /// </summary>
    public class WorkloadManifest
    {
        public WorkloadManifest(string id, long version, string? description, Dictionary<WorkloadDefinitionId, WorkloadDefinition> workloads, Dictionary<WorkloadPackId, WorkloadPack> packs, Dictionary<string, long>? dependsOnManifests)
        {
            Id = id;
            Version = version;
            Description = description;
            Workloads = workloads;
            Packs = packs;
            DependsOnManifests = dependsOnManifests;
        }

        /// <summary>
        /// The ID of the manifest is its filename without the extension.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The version of the manifest. It is relative to the SDK band.
        /// </summary>
        public long Version { get; }

        public string? Description { get; }

        /// <summary>
        /// ID and minimum version for any other manifests that this manifest depends on. Use only for validating consistancy.
        /// </summary>
        public Dictionary<string, long>? DependsOnManifests { get; }

        public Dictionary<WorkloadDefinitionId, WorkloadDefinition> Workloads { get; }
        public Dictionary<WorkloadPackId, WorkloadPack> Packs { get; }
    }
}
