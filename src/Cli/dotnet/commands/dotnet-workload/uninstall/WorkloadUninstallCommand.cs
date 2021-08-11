// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.Uninstall
{
    internal class WorkloadUninstallCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly IReadOnlyCollection<WorkloadId> _workloadIds;
        private readonly IInstaller _workloadInstaller;
        private readonly ReleaseVersion _sdkVersion;

        public WorkloadUninstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string version = null)
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _workloadIds = parseResult.ValueForArgument<IEnumerable<string>>(WorkloadUninstallCommandParser.WorkloadIdArgument)
                .Select(workloadId => new WorkloadId(workloadId)).ToList().AsReadOnly();
            var dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.ValueForOption<string>(WorkloadUninstallCommandParser.VersionOption), version, dotnetPath);
            var verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadUninstallCommandParser.VerbosityOption);
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, _sdkVersion.ToString());
            workloadResolver ??= WorkloadResolver.Create(workloadManifestProvider, dotnetPath, _sdkVersion.ToString());
            nugetPackageDownloader ??= new NuGetPackageDownloader(new DirectoryPath(Path.GetTempPath()), filePermissionSetter: null, verboseLogger: new NullLogger());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, sdkFeatureBand, workloadResolver, verbosity, nugetPackageDownloader, dotnetPath);
        }

        public override int Execute()
        {
            try
            {
                _reporter.WriteLine();

                var featureBand = new SdkFeatureBand(_sdkVersion);
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
                var unrecognizedWorkloads = _workloadIds.Where(workloadId => !installedWorkloads.Contains(workloadId));
                if (unrecognizedWorkloads.Any())
                {
                    throw new Exception(string.Format(LocalizableStrings.WorkloadNotInstalled, string.Join(" ", unrecognizedWorkloads)));
                }

                foreach (var workloadId in _workloadIds)
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.RemovingWorkloadInstallationRecord, workloadId));
                    _workloadInstaller.GetWorkloadInstallationRecordRepository()
                        .DeleteWorkloadInstallationRecord(workloadId, featureBand);
                }

                if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    var installer = _workloadInstaller.GetPackInstaller();

                    installer.GarbageCollectInstalledWorkloadPacks();
                }
                else
                {
                    var installer = _workloadInstaller.GetWorkloadInstaller();
                    foreach (var workloadId in _workloadIds)
                    {
                        installer.UninstallWorkload(workloadId);
                    }
                }

                _reporter.WriteLine();
                _reporter.WriteLine(string.Format(LocalizableStrings.UninstallSucceeded, string.Join(" ", _workloadIds)));
                _reporter.WriteLine();
            }
            catch (Exception e)
            {
                _workloadInstaller.Shutdown();
                // Don't show entire stack trace
                throw new GracefulException(string.Format(LocalizableStrings.WorkloadUninstallFailed, e.Message), e);
            }

            return _workloadInstaller.ExitCode;
        }
    }
}
