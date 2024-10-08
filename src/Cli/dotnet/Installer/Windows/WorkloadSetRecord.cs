// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a workload set that has been installed
    /// </summary>
    internal class WorkloadSetRecord
    {
        /// <summary>
        /// The dependency provider key of the workload set MSI used for reference counting shared installations.
        /// </summary>
        public string ProviderKeyName
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the workload set installed by this MSI
        /// </summary>
        public string WorkloadSetVersion
        {
            get;
            set;
        }

        public string WorkloadSetFeatureBand
        {
            get;
            set;
        }

        public string WorkloadSetPackageVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The product code (GUID) of the workload set MSI.
        /// </summary>
        public string ProductCode
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the workload set MSI.
        /// </summary>
        public Version ProductVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The upgrade code (GUID) of the workload set MSI.
        /// </summary>
        public string UpgradeCode
        {
            get;
            set;
        }
    }
}
