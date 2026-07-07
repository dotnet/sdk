// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal class WorkloadSearchCommand : WorkloadCommandBase
{
    private readonly IWorkloadResolver _workloadResolver;
    private readonly string _workloadIdStub;

    public WorkloadSearchCommand(
        ParseResult result,
        IReporter reporter = null,
        IWorkloadResolverFactory workloadResolverFactory = null) : base(result, CommonOptions.HiddenVerbosityOption, reporter)
    {
        _workloadIdStub = result.GetValue(WorkloadSearchCommandParser.WorkloadIdStubArgument);

        workloadResolverFactory ??= new WorkloadResolverFactory();

        if (!string.IsNullOrEmpty(result.GetValue(WorkloadSearchCommandParser.VersionOption)))
        {
            throw new GracefulException(CliCommandStrings.SdkVersionOptionNotSupported);
        }

        var creationResult = workloadResolverFactory.Create();

        _workloadResolver = creationResult.WorkloadResolver;
    }

    public override int Execute()
    {
        IEnumerable<WorkloadResolver.WorkloadInfo> availableWorkloads = _workloadResolver.GetAvailableWorkloads()
            .OrderBy(workload => workload.Id);

        if (!string.IsNullOrEmpty(_workloadIdStub))
        {
            availableWorkloads = availableWorkloads
                .Where(workload => workload.Id.ToString().Contains(_workloadIdStub, StringComparison.OrdinalIgnoreCase) || (workload.Description?.Contains(_workloadIdStub, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var table = new PrintableTable<WorkloadResolver.WorkloadInfo>();
        table.AddColumn(CliCommandStrings.WorkloadIdColumnName, workload => workload.Id.ToString());
        table.AddColumn(CliCommandStrings.DescriptionColumnName, workload => workload.Description);

        Reporter.WriteLine();
        table.PrintRows(availableWorkloads, l => Reporter.WriteLine(l));
        Reporter.WriteLine();

        return 0;
    }
}
