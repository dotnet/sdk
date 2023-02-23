// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class WorkloadHistoryRecord
    {
        public DateTimeOffset TimeStarted { get; set; }
        public DateTimeOffset TimeCompleted { get; set; }

        public string CommandName { get; set; }

        public List<string> WorkloadArguments { get; set; }

        public Dictionary<string, string> RollbackFileContents { get; set; }

        public string[] CommandLineArgs { get; set; }

        public bool Succeeded { get; set; }

        public string ErrorMessage { get; set; }

        public WorkloadHistoryState StateBeforeCommand { get; set; }

        public WorkloadHistoryState StateAfterCommand { get; set; }
    }

    internal class WorkloadHistoryState
    {
        public Dictionary<string, string> ManifestVersions { get; set; }

        public List<string> InstalledWorkloads { get; set; }

        //  Possibly we should add this in the future, but it requires changes to the IAL which to some degree break the abstraction
        //public List<(string id, string version)> InstalledWorkloadPacks { get; set;}

        public bool Equals(WorkloadHistoryState other)
        {
            if (ManifestVersions.Count != other.ManifestVersions.Count)
            {
                return false;
            }
            foreach (var manifestId in ManifestVersions.Keys)
            {
                if (!other.ManifestVersions.TryGetValue(manifestId, out string otherManifestVersion) ||
                    ManifestVersions[manifestId] != otherManifestVersion)
                {
                    return false;
                }
            }

            return new HashSet<string>(InstalledWorkloads).SetEquals(other.InstalledWorkloads);
        }

        public override bool Equals(object other)
        {
            if (other is WorkloadHistoryState otherState)
            {
                return Equals(otherState);
            }
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = new HashCode();
            foreach (var kvp in ManifestVersions)
            {
                hc.Add(kvp.Key);
                hc.Add(kvp.Value);
            }

            foreach (var workload in InstalledWorkloads)
            {
                hc.Add(workload);
            }

            return hc.ToHashCode();
        }
    }
}
