// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.DotNetWorkloads;

namespace Microsoft.DotNet.Workloads.Workload.History
{
    internal class WorkloadHistoryCommand : WorkloadCommandBase
    {
        private readonly IInstaller _workloadInstaller;
        private IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _dotnetPath;

        public WorkloadHistoryCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string tempDirPath = null,
            string version = null,
            string userProfileDir = null
        ) : base(parseResult, CommonOptions.HiddenVerbosityOption, reporter, tempDirPath, nugetPackageDownloader)
        {
            var creationResult = new WorkloadResolverFactory().Create();

            _dotnetPath = creationResult.DotnetPath;
            userProfileDir = creationResult.UserProfileDir;
            _sdkVersion = creationResult.SdkVersion;
            _workloadResolver = creationResult.WorkloadResolver;
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);

            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand,
                                     _workloadResolver, Verbosity, userProfileDir, VerifySignatures, PackageDownloader, dotnetDir, TempDirectoryPath,
                                     packageSourceLocation: null, _parseResult.ToRestoreActionConfig());
        }

        public override int Execute()
        {
            var displayRecords = WorkloadHistoryDisplay.ProcessWorkloadHistoryRecords(_workloadInstaller.GetWorkloadHistoryRecords());

            if (!displayRecords.Any())
            {
                Reporter.WriteLine(LocalizableStrings.NoHistoryFound);
            }

            var table = new PrintableTable<WorkloadHistoryDisplay.DisplayRecord>();
            table.AddColumn(LocalizableStrings.Id, r => r.ID?.ToString() ?? "");
            table.AddColumn(LocalizableStrings.Date, r => r.TimeStarted?.ToString() ?? "");
            table.AddColumn(LocalizableStrings.Command, r => r.Command);
            table.AddColumn(LocalizableStrings.Workloads, r => r.Workloads == null ? "" : string.Join(", ", r.Workloads));

            Reporter.WriteLine();
            table.PrintRows(displayRecords, l => Reporter.WriteLine(l));
            Reporter.WriteLine();

            return 0;
        }
       
    }
}
