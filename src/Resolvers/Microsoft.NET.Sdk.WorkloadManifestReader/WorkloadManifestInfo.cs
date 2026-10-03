// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
#if TEMPLATE_LOCATOR_PUBLIC_WORKLOAD_API
    public
#endif
    partial class WorkloadManifestInfo
    {
#if TEMPLATE_LOCATOR_PUBLIC_WORKLOAD_API
        internal
#else
        public
#endif
        WorkloadManifestInfo(string id, string version, string manifestDirectory, string manifestFeatureBand)
        {
            Id = id;
            Version = version;
            ManifestDirectory = manifestDirectory;
            ManifestFeatureBand = manifestFeatureBand;
        }

        public string Id { get; }
        public string Version { get; }
#if TEMPLATE_LOCATOR_PUBLIC_WORKLOAD_API
        internal
#else
        public
#endif
        string ManifestDirectory { get; }
#if TEMPLATE_LOCATOR_PUBLIC_WORKLOAD_API
        internal
#else
        public
#endif
        string ManifestFeatureBand { get; }
    }
}
