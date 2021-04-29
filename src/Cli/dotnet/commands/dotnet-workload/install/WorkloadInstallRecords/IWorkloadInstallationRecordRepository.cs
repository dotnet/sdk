// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal interface IWorkloadInstallationRecordRepository
    {
        IEnumerable<string> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand);

        void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);

        void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);

        IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords();
    }
}
