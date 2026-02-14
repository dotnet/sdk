// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// Provides a hook for the CLI layer to detect and repair corrupt workload manifest installations
    /// before the manifests are loaded by the resolver.
    /// </summary>
    public interface IWorkloadManifestCorruptionRepairer
    {
        /// <summary>
        /// Ensures that the manifests required by the current resolver are present and healthy.
        /// </summary>
        /// <param name="failureMode">How to handle corruption if detected.</param>
        void EnsureManifestsHealthy(ManifestCorruptionFailureMode failureMode);
    }
}
