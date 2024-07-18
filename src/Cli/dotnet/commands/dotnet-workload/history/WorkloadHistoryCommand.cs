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
        private readonly SdkFeatureBand _sdkFeatureBand;

        public WorkloadHistoryCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string tempDirPath = null
        ) : base(parseResult, CommonOptions.HiddenVerbosityOption, reporter, tempDirPath, nugetPackageDownloader)
        {
            var creationResult = new WorkloadResolverFactory().Create();

            var userProfileDir = creationResult.UserProfileDir;
            _sdkVersion = creationResult.SdkVersion;
            _workloadResolver = creationResult.WorkloadResolver;
            _sdkFeatureBand = new SdkFeatureBand(_sdkVersion);

            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, _sdkFeatureBand,
                                     _workloadResolver, Verbosity, userProfileDir, VerifySignatures, PackageDownloader, dotnetDir, TempDirectoryPath,
                                     packageSourceLocation: null, _parseResult.ToRestoreActionConfig());
        }

        public override int Execute()
        {
            var displayRecords = WorkloadHistoryDisplay.ProcessWorkloadHistoryRecords(_workloadInstaller.GetWorkloadHistoryRecords(_sdkFeatureBand.ToString()), out bool unknownRecordsPresent);

            if (!displayRecords.Any())
            {
                Reporter.WriteLine(LocalizableStrings.NoHistoryFound);
            }
            else
            {
                displayRecords.Insert(0, new WorkloadHistoryDisplay.DisplayRecord()
                {
                    TimeStarted = DateTimeOffset.MinValue,
                    ID = 1,
                    Command = "InitialState",
                    Workloads = displayRecords.First()?.HistoryRecord?.StateBeforeCommand?.InstalledWorkloads,
                });
                var table = new PrintableTable<WorkloadHistoryDisplay.DisplayRecord>();
                table.AddColumn(LocalizableStrings.Id, r => r.ID?.ToString() ?? "");
                table.AddColumn(LocalizableStrings.Date, r => r.TimeStarted?.ToString() ?? "");
                table.AddColumn(LocalizableStrings.Command, r => r.Command);
                table.AddColumn(LocalizableStrings.Workloads, r => string.Join(", ", r.Command.Equals("InitialState") ? displayRecords[1].HistoryRecord.StateBeforeCommand.InstalledWorkloads : r.HistoryRecord.StateAfterCommand.InstalledWorkloads));
                table.AddColumn(LocalizableStrings.GlobalJsonVersion, r => r.GlobalJsonVersion ?? string.Empty);

                Reporter.WriteLine();
                table.PrintRows(displayRecords, l => Reporter.WriteLine(l));
                Reporter.WriteLine();

                if (unknownRecordsPresent)
                {
                    Reporter.WriteLine("Rows with Unlogged changes represent changes from actions other than .NET CLI workload commands. Usually this represents an update to the .NET SDK or to Visual Studio."); // todo: localize
                    Reporter.WriteLine();
                }
            }

            return 0;
        }

    }
}