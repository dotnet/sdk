// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal interface IWorkloadInfoHelper : IWorkloadsRepositoryEnumerator
{
    IInstaller Installer { get; }
    IWorkloadInstallationRecordRepository WorkloadRecordRepo { get; }
    IWorkloadResolver WorkloadResolver { get; }
    void CheckTargetSdkVersionIsValid();
}
