// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Uninstall
{
    internal class WorkloadUninstallCommand : WorkloadCommandBase
    {
        private readonly IReadOnlyCollection<WorkloadId> _workloadIds;
        private readonly IInstaller _workloadInstaller;
        protected readonly IWorkloadResolverFactory _workloadResolverFactory;
        private readonly string _dotnetPath;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _userProfileDir;
        private readonly WorkloadHistoryRecorder _recorder;

        public WorkloadUninstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null,
            INuGetPackageDownloader nugetPackageDownloader = null)
            : base(parseResult, reporter: reporter, nugetPackageDownloader: nugetPackageDownloader)
        {
            _workloadIds = parseResult.GetValue(WorkloadUninstallCommandParser.WorkloadIdArgument)
                .Select(workloadId => new WorkloadId(workloadId)).ToList().AsReadOnly();

            _workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

            if (!string.IsNullOrEmpty(parseResult.GetValue(WorkloadUninstallCommandParser.VersionOption)))
            {
                throw new GracefulException(Install.LocalizableStrings.SdkVersionOptionNotSupported);
            }

            var creationResult = _workloadResolverFactory.Create();

            _dotnetPath = creationResult.DotnetPath;
            _sdkVersion = creationResult.SdkVersion;
            _userProfileDir = creationResult.UserProfileDir;

            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand, creationResult.WorkloadResolver, Verbosity, creationResult.UserProfileDir, VerifySignatures, PackageDownloader, creationResult.DotnetPath);
            _recorder = new(_workloadResolverFactory.Create().WorkloadResolver, _workloadInstaller, () => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, null));
            _recorder.HistoryRecord.CommandName = "uninstall";
        }

        public override int Execute()
        {
            try
            {
                _recorder.Run(() =>
                {
                    Reporter.WriteLine();

                    var featureBand = new SdkFeatureBand(_sdkVersion);
                    var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
                    var unrecognizedWorkloads = _workloadIds.Where(workloadId => !installedWorkloads.Contains(workloadId));
                    if (unrecognizedWorkloads.Any())
                    {
                        throw new Exception(string.Format(LocalizableStrings.WorkloadNotInstalled, string.Join(" ", unrecognizedWorkloads)));
                    }

                    foreach (var workloadId in _workloadIds)
                    {
                        Reporter.WriteLine(string.Format(LocalizableStrings.RemovingWorkloadInstallationRecord, workloadId));
                        _workloadInstaller.GetWorkloadInstallationRecordRepository()
                            .DeleteWorkloadInstallationRecord(workloadId, featureBand);
                    }

                    _workloadInstaller.GarbageCollect(workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion));

                    Reporter.WriteLine();
                    Reporter.WriteLine(string.Format(LocalizableStrings.UninstallSucceeded, string.Join(" ", _workloadIds)));
                    Reporter.WriteLine();
                });
            }
            catch (Exception e)
            {
                _workloadInstaller.Shutdown();
                // Don't show entire stack trace
                throw new GracefulException(string.Format(LocalizableStrings.WorkloadUninstallFailed, e.Message), e, isUserError: false);
            }

            return _workloadInstaller.ExitCode;
        }
    }
}
