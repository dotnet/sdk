// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Repair;

internal class WorkloadRepairCommand : InstallingWorkloadCommand
{
    private WorkloadHistoryRecorder _recorder;

    public WorkloadRepairCommand(
        ParseResult parseResult,
        IReporter reporter = null,
        IWorkloadResolverFactory workloadResolverFactory = null,
        IInstaller workloadInstaller = null,
        INuGetPackageDownloader nugetPackageDownloader = null)
        : base(parseResult, reporter, workloadResolverFactory, workloadInstaller, nugetPackageDownloader, null, null, WorkloadRepairCommandParser.VerbosityOption)
    {
        // Initialize _workloadInstaller from the base class field or create a new one
        _workloadInstaller = _workloadInstallerFromConstructor ?? WorkloadInstallerFactory.GetWorkloadInstaller(
            Reporter,
            _sdkFeatureBand,
            _workloadResolver,
            Verbosity,
            _userProfileDir,
            VerifySignatures,
            PackageDownloader,
            _dotnetPath,
            TempDirectoryPath,
            packageSourceLocation: _packageSourceLocation,
            RestoreActionConfiguration);
    }

    private void EnsureRecorderInitialized()
    {
        if (_recorder == null)
        {
            _recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller, () => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, null));
            _recorder.HistoryRecord.CommandName = "repair";
        }
    }

    public override int Execute()
    {
        EnsureRecorderInitialized();

        try
        {
            _recorder.Run(() =>
            {
                Reporter.WriteLine();

                // Repair missing manifests first if workload set is corrupt
                var corruptWorkloadSet = GetCorruptWorkloadSetIfAny();
                if (corruptWorkloadSet != null)
                {
                    CliTransaction.RunNew(context => RepairCorruptWorkloadSet(context, corruptWorkloadSet));
                }

                var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);

                if (!workloadIds.Any())
                {
                    Reporter.WriteLine(CliCommandStrings.NoWorkloadsToRepair);
                    return;
                }

                Reporter.WriteLine(string.Format(CliCommandStrings.RepairingWorkloads, string.Join(" ", workloadIds)));

                ReinstallWorkloadsBasedOnCurrentManifests(workloadIds);

                WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion));

                Reporter.WriteLine();
                Reporter.WriteLine(string.Format(CliCommandStrings.RepairSucceeded, string.Join(" ", workloadIds)));
                Reporter.WriteLine();
            });
        }
        catch (Exception e)
        {
            // Don't show entire stack trace
            throw new GracefulException(string.Format(CliCommandStrings.WorkloadRepairFailed, e.Message), e, isUserError: false);
        }
        finally
        {
            _workloadInstaller.Shutdown();
        }

        return _workloadInstaller.ExitCode;
    }

    private void ReinstallWorkloadsBasedOnCurrentManifests(IEnumerable<WorkloadId> workloadIds)
    {
        _workloadInstaller.RepairWorkloads(workloadIds, _sdkFeatureBand);
    }
}
