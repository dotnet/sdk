// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Configurer;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.commands.dotnet_workload;

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
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            userProfileDir ??= CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValue(WorkloadRepairCommandParser.VersionOption), version, _dotnetPath, userProfileDir, true);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), userProfileDir);
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
                //  TODO: Localize
                Reporter.WriteLine("No workload history found");
            }


            //  TODO:
            //  - Get workload history records
            //  - Sort
            //  - Compare adjacent records and create unknown records if necessary
            //  - Display

            var table = new PrintableTable<WorkloadHistoryDisplay.DisplayRecord>();
            //  TODO: Localize column names
            table.AddColumn("ID", r => r.ID?.ToString() ?? "");
            //  TODO: How to format date?
            table.AddColumn("Date", r => r.TimeStarted?.ToString() ?? "");
            table.AddColumn("Command", r => r.Command);
            //  TODO: Do we localize the separator between workloads?
            table.AddColumn("Workloads", r => r.Workloads == null ? "" : string.Join(", ", r.Workloads));

            Reporter.WriteLine();
            table.PrintRows(displayRecords, l => Reporter.WriteLine(l));
            Reporter.WriteLine();

            return 0;
        }
       
    }
}
