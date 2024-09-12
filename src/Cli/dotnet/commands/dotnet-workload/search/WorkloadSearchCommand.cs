// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchCommand : WorkloadCommandBase
    {
        private readonly IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _workloadIdStub;
        private readonly int _numberOfWorkloadSetsToTake;
        private readonly string _workloadSetOutputFormat;
        private readonly IWorkloadManifestInstaller _installer;
        internal bool ListWorkloadSetVersions { get; set; } = false;

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

            _numberOfWorkloadSetsToTake = result.GetValue(SearchWorkloadSetsParser.TakeOption);
            _workloadSetOutputFormat = result.GetValue(SearchWorkloadSetsParser.FormatOption);

            _installer = WorkloadInstallerFactory.GetWorkloadInstaller(
                reporter,
                new SdkFeatureBand(_sdkVersion),
                _workloadResolver,
                Verbosity,
                creationResult.UserProfileDir,
                !SignCheck.IsDotNetSigned(),
                restoreActionConfig: new RestoreActionConfig(result.HasOption(SharedOptions.InteractiveOption)),
                elevationRequired: false,
                shouldLog: false);
        }

        public override int Execute()
        {
            if (ListWorkloadSetVersions)
            {
                var featureBand = new SdkFeatureBand(_sdkVersion);
                var packageId = _installer.GetManifestPackageId(new ManifestId("Microsoft.NET.Workloads"), featureBand);
                var versions = PackageDownloader.GetLatestPackageVersions(packageId, _numberOfWorkloadSetsToTake, packageSourceLocation: null, includePreview: !string.IsNullOrWhiteSpace(_sdkVersion.Prerelease))
                    .GetAwaiter().GetResult()
                    .Select(version => WorkloadManifestUpdater.WorkloadSetPackageVersionToWorkloadSetVersion(featureBand, version.Version.ToString()));
                if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Reporter.WriteLine(JsonSerializer.Serialize(versions.Select(version => version.ToDictionary(_ => "workloadVersion", v => v))));
                }
                else
                {
                    Reporter.WriteLine(string.Join('\n', versions));
                }

                return 0;
            }

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
