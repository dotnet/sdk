// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Workloads.Workload;
using WorkloadSetVersionUtil = Microsoft.DotNet.Workloads.Workload.WorkloadSetVersion;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetWorkloadSetFeatureBand : Task
    {
        [Required]
        public string WorkloadSetVersion { get; set; }

        [Output]
        public string WorkloadSetFeatureBand { get; set; }

        public override bool Execute()
        {
            WorkloadSetFeatureBand = WorkloadSetVersionUtil.GetFeatureBand(WorkloadSetVersion).ToString();
            return true;
        }
    }
}
