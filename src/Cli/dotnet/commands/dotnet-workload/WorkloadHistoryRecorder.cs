// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class WorkloadHistoryRecorder
    {
        public WorkloadHistoryRecord HistoryRecord { get; set; } = new();

        IWorkloadResolver _workloadResolver;
        IInstaller _workloadInstaller;

        public WorkloadHistoryRecorder(IWorkloadResolver workloadResolver, IInstaller workloadInstaller)
        {
            _workloadResolver = workloadResolver;
            _workloadInstaller = workloadInstaller;

            HistoryRecord.CommandLineArgs = Environment.GetCommandLineArgs();
        }

        public void Run(Action workloadAction)
        {
            HistoryRecord.TimeStarted = DateTimeOffset.Now;
            HistoryRecord.StateBeforeCommand = GetWorkloadState();

            try
            {
                workloadAction();

                HistoryRecord.Succeeded = true;
            }
            catch (Exception ex)
            {
                HistoryRecord.Succeeded = false;
                HistoryRecord.ErrorMessage = ex.ToString();
                throw;
            }
            finally
            {
                HistoryRecord.StateAfterCommand = GetWorkloadState();
                HistoryRecord.TimeCompleted = DateTimeOffset.Now;

                _workloadInstaller.WriteWorkloadHistoryRecord(HistoryRecord, _workloadInstaller.SdkFeatureBand.ToString());
            }
        }

        private WorkloadHistoryState GetWorkloadState()
        {
            return new WorkloadHistoryState()
            {
                ManifestVersions = WorkloadRollbackInfo.FromManifests(_workloadResolver.GetInstalledManifests()).ToDictionaryForJson(),
                InstalledWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                                       .GetInstalledWorkloads(new SdkFeatureBand(_workloadResolver.GetSdkFeatureBand()))
                                                       .Select(id => id.ToString())
                                                       .ToList()
            };
            
        }
    }
}
