// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal interface IWorkloadInfoHelper : IWorkloadsRepositoryEnumerator
    {
        IInstaller Installer { get; }
        IWorkloadInstallationRecordRepository WorkloadRecordRepo { get; }
        IWorkloadResolver WorkloadResolver { get; }
        void CheckTargetSdkVersionIsValid();
    }
}
