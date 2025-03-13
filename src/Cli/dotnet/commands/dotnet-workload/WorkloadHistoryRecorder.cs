// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload;

internal class WorkloadHistoryRecorder
{
    public WorkloadHistoryRecord HistoryRecord { get; set; } = new();

    IWorkloadResolver _workloadResolver;
    IInstaller _workloadInstaller;
    Func<IWorkloadResolver> _workloadResolverFunc;

    public WorkloadHistoryRecorder(IWorkloadResolver workloadResolver, IInstaller workloadInstaller, Func<IWorkloadResolver> workloadResolverFunc)
    {
        _workloadResolver = workloadResolver;
        _workloadInstaller = workloadInstaller;
        _workloadResolverFunc = workloadResolverFunc;

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

            _workloadInstaller.WriteWorkloadHistoryRecord(HistoryRecord, _workloadResolver.GetSdkFeatureBand());
        }
    }

    private WorkloadHistoryState GetWorkloadState()
    {
        var resolver = _workloadResolverFunc();
        var currentWorkloadVersion = resolver.GetWorkloadVersion().Version;
        return new WorkloadHistoryState()
        {
            ManifestVersions = resolver.GetInstalledManifests().ToDictionary(manifest => manifest.Id.ToString(), manifest => $"{manifest.Version}/{manifest.ManifestFeatureBand}"),
            InstalledWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                                   .GetInstalledWorkloads(new SdkFeatureBand(_workloadResolver.GetSdkFeatureBand()))
                                                   .Select(id => id.ToString())
                                                   .ToList(),
            WorkloadSetVersion = currentWorkloadVersion
        };

    }
}
