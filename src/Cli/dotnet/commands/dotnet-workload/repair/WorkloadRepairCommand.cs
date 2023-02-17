// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Workloads.Workload.Repair
{
    internal class WorkloadRepairCommand : WorkloadCommandBase
    {
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly IInstaller _workloadInstaller;
        protected readonly IWorkloadResolverFactory _workloadResolverFactory;
        private IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _dotnetPath;
        private readonly string _userProfileDir;

        public WorkloadRepairCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null)
            : base(parseResult, reporter: reporter, nugetPackageDownloader: nugetPackageDownloader)
        {
            var configOption = parseResult.GetValue(WorkloadRepairCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValue(WorkloadRepairCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            _workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

            if (!string.IsNullOrEmpty(parseResult.GetValue(WorkloadRepairCommandParser.VersionOption)))
            {
                throw new GracefulException(Install.LocalizableStrings.SdkVersionOptionNotSupported);
            }

            var creationResult = _workloadResolverFactory.Create();

            _dotnetPath = creationResult.DotnetPath;
            _sdkVersion = creationResult.SdkVersion;
            _userProfileDir = creationResult.UserProfileDir;
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadResolver = creationResult.WorkloadResolver;

            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand,
                                     _workloadResolver, Verbosity, creationResult.UserProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                     _packageSourceLocation, _parseResult.ToRestoreActionConfig());
        }

        public override int Execute()
        {
            WorkloadHistoryRecorder recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller);
            recorder.HistoryRecord.CommandName = "uninstall";

            try
            {
                recorder.Run(() =>
                {
                    try
                    {
                        Reporter.WriteLine();

                        var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));

                        if (!workloadIds.Any())
                        {
                            Reporter.WriteLine(LocalizableStrings.NoWorkloadsToRepair);
                            return;
                        }

                        Reporter.WriteLine(string.Format(LocalizableStrings.RepairingWorkloads, string.Join(" ", workloadIds)));

                        ReinstallWorkloadsBasedOnCurrentManifests(workloadIds, new SdkFeatureBand(_sdkVersion));

                        WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity);

                        Reporter.WriteLine();
                        Reporter.WriteLine(string.Format(LocalizableStrings.RepairSucceeded, string.Join(" ", workloadIds)));
                        Reporter.WriteLine();
                    }
                    catch (Exception e)
                    {
                        // Don't show entire stack trace
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadRepairFailed, e.Message), e, isUserError: false);
                    }
                });
            }
            finally
            {
                _workloadInstaller.Shutdown();
            }

            return _workloadInstaller.ExitCode;
        }

        private void ReinstallWorkloadsBasedOnCurrentManifests(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand)
        {
            _workloadInstaller.RepairWorkloads(workloadIds, sdkFeatureBand);
        }
    }
}
