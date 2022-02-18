// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class InstalledWorkloadsCollection : Dictionary<string, string>
    {
        /// <summary>
        /// Creates a new <see cref="InstalledWorkloadsCollection"/> instance using a collection of workload IDs
        /// and a common installation source.
        /// </summary>
        /// <param name="workloadIds">A collection of workload identifiers.</param>
        /// <param name="installationSource">A string describing the installation source of the workload identifiers.</param>
        public InstalledWorkloadsCollection(IEnumerable<WorkloadId> workloadIds, string installationSource) :
            base(workloadIds.Select(id => new KeyValuePair<string, string>(id.ToString(), installationSource)))
        {

        }

        /// <summary>
        /// Adds a new workload ID and installation source. If the ID already exists, the source is appended.
        /// </summary>
        /// <param name="workloadId">The ID of the workload to update.</param>
        /// <param name="installationSource">A string describing the installation soruce of the workload.</param>
        public new void Add(string workloadId, string installationSource)
        {
            if (!ContainsKey(workloadId))
            {
                this[workloadId] = installationSource;
            }
            else
            {
                this[workloadId] += $", {installationSource}";
            }
        }
    }
}
