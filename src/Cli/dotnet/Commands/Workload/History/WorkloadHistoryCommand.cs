// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.History;

internal class WorkloadHistoryCommand : WorkloadCommandBase
{
    private readonly IInstaller _workloadInstaller;
    private readonly IWorkloadResolver _workloadResolver;
    private readonly ReleaseVersion _sdkVersion;
    private readonly SdkFeatureBand _sdkFeatureBand;

    public WorkloadHistoryCommand(
        ParseResult parseResult,
        IReporter reporter = null,
        IInstaller workloadInstaller = null,
        INuGetPackageDownloader nugetPackageDownloader = null
    ) : base(parseResult, CommonOptions.HiddenVerbosityOption, reporter, null, nugetPackageDownloader)
    {
        var creationResult = new WorkloadResolverFactory().Create();

        var userProfileDir = creationResult.UserProfileDir;
        _sdkVersion = creationResult.SdkVersion;
        _workloadResolver = creationResult.WorkloadResolver;
        _sdkFeatureBand = new SdkFeatureBand(_sdkVersion);

        _workloadInstaller = workloadInstaller ??
                             WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, _sdkFeatureBand,
                                 _workloadResolver, Verbosity, userProfileDir, VerifySignatures, PackageDownloader, creationResult.DotnetPath, TempDirectoryPath,
                                 packageSourceLocation: null, _parseResult.ToRestoreActionConfig());
    }

    public override int Execute()
    {
        var displayRecords = WorkloadHistoryDisplay.ProcessWorkloadHistoryRecords(_workloadInstaller.GetWorkloadHistoryRecords(_sdkFeatureBand.ToString()), out bool unknownRecordsPresent);

        if (displayRecords.Count == 0)
        {
            Reporter.WriteLine(CliCommandStrings.NoHistoryFound);
        }
        else
        {
            var table = new PrintableTable<WorkloadHistoryDisplay.DisplayRecord>();
            table.AddColumn(CliCommandStrings.Id, r => r.ID?.ToString() ?? "");
            table.AddColumn(CliCommandStrings.Date, r => r.TimeStarted?.ToString() ?? "");
            table.AddColumn(CliCommandStrings.Command, r => r.Command);
            table.AddColumn(CliCommandStrings.Workloads, r => string.Join(", ", r.HistoryState.InstalledWorkloads ?? []));
            table.AddColumn(CliCommandStrings.GlobalJsonVersion, r => r.GlobalJsonVersion ?? string.Empty);
            table.AddColumn(CliCommandStrings.WorkloadHistoryWorkloadSetVersion, r => r.HistoryState.WorkloadSetVersion ?? string.Empty);

            Reporter.WriteLine();
            table.PrintRows(displayRecords, l => Reporter.WriteLine(l));
            Reporter.WriteLine();

            if (unknownRecordsPresent)
            {
                Reporter.WriteLine(CliCommandStrings.UnknownRecordsInformationalMessage);
                Reporter.WriteLine();
            }
        }

        return 0;
    }

}
