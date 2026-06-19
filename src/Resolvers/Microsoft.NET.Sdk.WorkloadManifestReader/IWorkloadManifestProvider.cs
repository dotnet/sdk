// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// Specifies how the manifest provider should handle corrupt or missing workload manifests.
    /// </summary>
    public enum ManifestCorruptionFailureMode
    {
        /// <summary>
        /// Attempt to repair using the CorruptionRepairer if available, otherwise throw.
        /// This is the default mode for commands that modify workloads.
        /// </summary>
        Repair,

        /// <summary>
        /// Throw a helpful error message suggesting how to fix the issue.
        /// Use this for read-only/info commands.
        /// </summary>
        Throw,

        /// <summary>
        /// Silently ignore missing manifests and continue.
        /// Use this for history recording or other scenarios where missing manifests are acceptable.
        /// </summary>
        Ignore
    }

    /// <summary>
    /// This abstracts out the process of locating and loading a set of manifests to be loaded into a
    /// workload manifest resolver and resolved into a single coherent model.
    /// </summary>
    public interface IWorkloadManifestProvider
    {
        void RefreshWorkloadManifests();
        IEnumerable<ReadableWorkloadManifest> GetManifests();

        string GetSdkFeatureBand();

        WorkloadVersionInfo GetWorkloadVersion();

        Dictionary<string, WorkloadSet> GetAvailableWorkloadSets();

        public readonly record struct WorkloadVersionInfo(string Version, bool IsInstalled = true, bool WorkloadSetsEnabledWithoutWorkloadSet = false, string? GlobalJsonPath = null, bool? GlobalJsonSpecifiesWorkloadSets = null);
    }

    public record WorkloadVersion
    {
        public enum Type
        {
            WorkloadSet,
            LooseManifest
        }

        public string? Version;
        public Type WorkloadInstallType;
    }
}
