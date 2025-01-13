// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

using NuGet.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a workload manifest that has been installed
    /// </summary>
    internal class WorkloadManifestRecord
    {
        /// <summary>
        /// The dependency provider key of the workload pack MSI used for reference counting shared installations.
        /// </summary>
        public string ProviderKeyName
        {
            get;
            set;
        }

        /// <summary>
        /// The Manifest ID, such as Microsoft.NET.Workload.Mono.ToolChain.Current.  This ID does NOT include the ".Manifest-{FeatureBand}" or ".Msi.{HostArchitecture}" suffix
        /// </summary>
        public string ManifestId
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the manifest installed by this MSI
        /// </summary>
        public string ManifestVersion
        {
            get;
            set;
        }

        public string ManifestFeatureBand
        {
            get;
            set;
        }

        /// <summary>
        /// The product code (GUID) of the workload manifest MSI.
        /// </summary>
        public string ProductCode
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the workload manifest MSI.
        /// </summary>
        public Version ProductVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The upgrade code (GUID) of the workload pack MSI.
        /// </summary>
        public string UpgradeCode
        {
            get;
            set;
        }
    }
}
