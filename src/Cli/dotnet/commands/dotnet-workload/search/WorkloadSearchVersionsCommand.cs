// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

using InformationStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchVersionsCommand : WorkloadCommandBase
    {
        private readonly ReleaseVersion _sdkVersion;
        private readonly int _numberOfWorkloadSetsToTake;
        private readonly string _workloadSetOutputFormat;
        private readonly FileBasedInstaller _installer;
        private readonly string _workloadVersion;
        private readonly bool _includePreviews;

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
                restoreActionConfig: new RestoreActionConfig(result.HasOption(SharedOptions.InteractiveOption)),
                nugetPackageDownloaderVerbosity: VerbosityOptions.quiet
                );

            _workloadVersion = result.GetValue(WorkloadSearchVersionsCommandParser.WorkloadVersionArgument);

            _includePreviews = result.HasOption(WorkloadSearchVersionsCommandParser.IncludePreviewsOption) ?
                result.GetValue(WorkloadSearchVersionsCommandParser.IncludePreviewsOption) :
                new SdkFeatureBand(_sdkVersion).IsPrerelease;
        }

        public override int Execute()
        {
            if (_workloadVersion is null)
            {
                var featureBand = new SdkFeatureBand(_sdkVersion);
                var packageId = _installer.GetManifestPackageId(new ManifestId("Microsoft.NET.Workloads"), featureBand);

                List<string> versions;
                try
                {
                    versions = PackageDownloader.GetLatestPackageVersions(packageId, _numberOfWorkloadSetsToTake, packageSourceLocation: null, includePreview: _includePreviews)
                        .GetAwaiter().GetResult()
                        .Select(version => WorkloadSetVersion.FromWorkloadSetPackageVersion(featureBand, version.ToString()))
                        .ToList();
                }
                catch (NuGetPackageNotFoundException)
                {
                    Microsoft.DotNet.Cli.Utils.Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoWorkloadVersionsFound, featureBand));
                    return 0;
                }
                if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Reporter.WriteLine(JsonSerializer.Serialize(versions.Select(version => version.ToDictionary(_ => "workloadVersion", v => v))));
                }
                else
                {
                    Reporter.WriteLine(string.Join('\n', versions));
                }
            }
            else
            {
                var workloadSet = _installer.GetWorkloadSetContents(_workloadVersion);
                if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var set = new WorkloadSet() { ManifestVersions = workloadSet.ManifestVersions };
                    Reporter.WriteLine(JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "manifestVersions", set.ToDictionaryForJson() }
                    }, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    PrintableTable<KeyValuePair<ManifestId, (ManifestVersion Version, SdkFeatureBand FeatureBand)>> table = new();
                    table.AddColumn(LocalizableStrings.WorkloadManifestIdColumn, manifest => manifest.Key.ToString());
                    table.AddColumn(LocalizableStrings.WorkloadManifestFeatureBandColumn, manifest => manifest.Value.FeatureBand.ToString());
                    table.AddColumn(InformationStrings.WorkloadManifestVersionColumn, manifest => manifest.Value.Version.ToString());
                    table.PrintRows(workloadSet.ManifestVersions, l => Reporter.WriteLine(l));
                }
            }

            return 0;
        }
    }
}
