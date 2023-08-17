// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
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

            var creationParameters = new WorkloadResolverFactory.CreationParameters()
            {
                DotnetPath = dotnetDir,
                UserProfileDir = userProfileDir,
                GlobalJsonStartDir = null,
                SdkVersionFromOption = parseResult.SafelyGetValueForOption(InstallingWorkloadCommandParser.VersionOption),
                VersionForTesting = version,
                CheckIfFeatureBandManifestExists = !(parseResult.SafelyGetValueForOption(InstallingWorkloadCommandParser.PrintDownloadLinkOnlyOption)),
                WorkloadResolverForTesting = workloadResolver,
                UseInstalledSdkVersionForResolver = true
            };

            var creationResult = WorkloadResolverFactory.Create(creationParameters);

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
