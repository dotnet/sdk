// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Uninstall
{
    internal class WorkloadUninstallCommand : WorkloadCommandBase
    {
        private readonly IReadOnlyCollection<WorkloadId> _workloadIds;
        private readonly IInstaller _workloadInstaller;
        private readonly ReleaseVersion _sdkVersion;

        public WorkloadUninstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string version = null,
            string userProfileDir = null)
            : base(parseResult, reporter: reporter, nugetPackageDownloader: nugetPackageDownloader)
        {
            _workloadIds = parseResult.GetValue(WorkloadUninstallCommandParser.WorkloadIdArgument)
                .Select(workloadId => new WorkloadId(workloadId)).ToList().AsReadOnly();
            var dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValue(WorkloadUninstallCommandParser.VersionOption), version, dotnetPath, userProfileDir, true);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, _sdkVersion.ToString(), userProfileDir);
            workloadResolver ??= WorkloadResolver.Create(workloadManifestProvider, dotnetPath, _sdkVersion.ToString(), userProfileDir);

            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand, workloadResolver, Verbosity, userProfileDir, VerifySignatures, PackageDownloader, dotnetPath);
        }

        public override int Execute()
        {
            try
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

                _workloadInstaller.GarbageCollectInstalledWorkloadPacks();

                Reporter.WriteLine();
                Reporter.WriteLine(string.Format(LocalizableStrings.UninstallSucceeded, string.Join(" ", _workloadIds)));
                Reporter.WriteLine();
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
