// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetWorkloadSetFeatureBand : Task
    {
        [Required]
        public string WorkloadSetVersion { get; set; }

        [Output]
        public string WorkloadSetFeatureBand { get; set; }

        public override bool Execute()
        {
            WorkloadSetFeatureBand = Cli.WorkloadSetVersion.GetFeatureBand(WorkloadSetVersion).ToString();
            return true;
        }
    }
}
