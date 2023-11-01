// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchCommand : WorkloadCommandBase
    {
        private readonly IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _workloadIdStub;

        public WorkloadSearchCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null) : base(result, CommonOptions.HiddenVerbosityOption, reporter)
        {
            _workloadIdStub = result.GetValue(WorkloadSearchCommandParser.WorkloadIdStubArgument);

            workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

            if (!string.IsNullOrEmpty(result.GetValue(WorkloadSearchCommandParser.VersionOption)))
            {
                throw new GracefulException(Install.LocalizableStrings.SdkVersionOptionNotSupported);
            }

            var creationResult = workloadResolverFactory.Create();

            _sdkVersion = creationResult.SdkVersion;
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
            table.AddColumn(LocalizableStrings.WorkloadIdColumnName, workload => workload.Id.ToString());
            table.AddColumn(LocalizableStrings.DescriptionColumnName, workload => workload.Description);

            Reporter.WriteLine();
            table.PrintRows(availableWorkloads, l => Reporter.WriteLine(l));
            Reporter.WriteLine();

            return 0;
        }
    }
}
