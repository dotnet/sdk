// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;
using NuGet.Versioning;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchVersionsCommand : WorkloadCommandBase
    {
        private readonly ReleaseVersion _sdkVersion;
        private readonly int _numberOfWorkloadSetsToTake;
        private readonly string _workloadSetOutputFormat;
        private readonly FileBasedInstaller _installer;
        private readonly string _workloadVersion;

        public WorkloadSearchVersionsCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null) : base(result, CommonOptions.HiddenVerbosityOption, reporter)
        {
            workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

            if (!string.IsNullOrEmpty(result.GetValue(WorkloadSearchCommandParser.VersionOption)))
            {
                throw new GracefulException(Install.LocalizableStrings.SdkVersionOptionNotSupported);
            }

            var creationResult = workloadResolverFactory.Create();

            _sdkVersion = creationResult.SdkVersion;
            var workloadResolver = creationResult.WorkloadResolver;

            _numberOfWorkloadSetsToTake = result.GetValue(WorkloadSearchVersionsCommandParser.TakeOption);
            _workloadSetOutputFormat = result.GetValue(WorkloadSearchVersionsCommandParser.FormatOption);

            // For these operations, we don't have to respect 'msi' because they're equivalent between the two workload
            // install types, and FileBased is much easier to work with.
            _installer = new FileBasedInstaller(
                reporter,
                new SdkFeatureBand(_sdkVersion),
                workloadResolver,
                CliFolderPathCalculator.DotnetUserProfileFolderPath,
                nugetPackageDownloader: null,
                dotnetDir: Path.GetDirectoryName(Environment.ProcessPath),
                tempDirPath: null,
                verbosity: Verbosity,
                packageSourceLocation: null,
                restoreActionConfig: new RestoreActionConfig(result.HasOption(SharedOptions.InteractiveOption))
                );

            _workloadVersion = result.GetValue(WorkloadSearchVersionsCommandParser.WorkloadVersionArgument);
        }

        public override int Execute()
        {
            if (_workloadVersion is null)
            {
                var featureBand = new SdkFeatureBand(_sdkVersion);
                var packageId = _installer.GetManifestPackageId(new ManifestId(WorkloadManifestUpdater.WorkloadSetManifestId), featureBand);
                var versions = PackageDownloader.GetLatestPackageVersions(packageId, _numberOfWorkloadSetsToTake, packageSourceLocation: null, includePreview: !string.IsNullOrWhiteSpace(_sdkVersion.Prerelease))
                    .GetAwaiter().GetResult()
                    .Select(version => WorkloadManifestUpdater.WorkloadSetPackageVersionToWorkloadSetVersion(featureBand, version.Version.ToString()));
                if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Reporter.WriteLine(JsonSerializer.Serialize(versions));
                }
                else
                {
                    Reporter.WriteLine(string.Join(',', versions));
                }
            }
            else
            {
                var workloadSet = _installer.GetWorkloadSetContents(_workloadVersion);
                if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Reporter.WriteLine(JsonSerializer.Serialize(workloadSet.ManifestVersions));
                }
                else
                {
                    Reporter.WriteLine(workloadSet.ManifestVersions.ToString());
                }
            }

            return 0;
        }
    }
}
