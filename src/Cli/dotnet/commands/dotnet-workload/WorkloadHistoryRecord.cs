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
    }
}
