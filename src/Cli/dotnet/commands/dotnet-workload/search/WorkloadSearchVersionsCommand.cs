// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;

using InformationStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchVersionsCommand : WorkloadCommandBase
    {
        private readonly ReleaseVersion _sdkVersion;
        private readonly int _numberOfWorkloadSetsToTake;
        private readonly string _workloadSetOutputFormat;
        private readonly IInstaller _installer;
        private readonly IEnumerable<string> _workloadVersion;
        private readonly bool _includePreviews;
        private readonly IWorkloadResolver _resolver;

        public WorkloadSearchVersionsCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null,
            IInstaller installer = null,
            INuGetPackageDownloader nugetPackageDownloader = null) : base(result, CommonOptions.HiddenVerbosityOption, reporter, nugetPackageDownloader: nugetPackageDownloader)
        {
            workloadResolverFactory ??= new WorkloadResolverFactory();

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
            _installer = installer ?? GenerateInstaller(Reporter, new SdkFeatureBand(_sdkVersion), workloadResolver, Verbosity, result.HasOption(SharedOptions.InteractiveOption));

            _workloadVersion = result.GetValue(WorkloadSearchVersionsCommandParser.WorkloadVersionArgument);

            _includePreviews = result.HasOption(WorkloadSearchVersionsCommandParser.IncludePreviewsOption) ?
                result.GetValue(WorkloadSearchVersionsCommandParser.IncludePreviewsOption) :
                new SdkFeatureBand(_sdkVersion).IsPrerelease;

            _resolver = creationResult.WorkloadResolver;
        }

        private static IInstaller GenerateInstaller(IReporter reporter, SdkFeatureBand sdkFeatureBand, IWorkloadResolver workloadResolver, VerbosityOptions verbosity, bool interactive)
        {
            return new FileBasedInstaller(
                reporter,
                sdkFeatureBand,
                workloadResolver,
                CliFolderPathCalculator.DotnetUserProfileFolderPath,
                nugetPackageDownloader: null,
                dotnetDir: Path.GetDirectoryName(Environment.ProcessPath),
                tempDirPath: null,
                verbosity: verbosity,
                packageSourceLocation: null,
                restoreActionConfig: new RestoreActionConfig(interactive),
                nugetPackageDownloaderVerbosity: VerbosityOptions.quiet
                );
        }

        public override int Execute()
        {
            if (_workloadVersion.Count() == 0)
            {
                List<string> versions;
                try
                {
                    versions = GetVersions(_numberOfWorkloadSetsToTake);
                }
                catch (NuGetPackageNotFoundException)
                {
                    Cli.Utils.Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoWorkloadVersionsFound, new SdkFeatureBand(_sdkVersion)));
                    return 0;
                }

                if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Reporter.WriteLine(JsonSerializer.Serialize(versions.Select(version => new Dictionary<string, string>()
                    {
                        { "workloadVersion", version }
                    })));
                }
                else
                {
                    Reporter.WriteLine(string.Join('\n', versions));
                }
            }
            else if (_workloadVersion.Any(v => v.Contains('@')))
            {
                var versions = FindBestWorkloadSetsFromComponents()?.Take(_numberOfWorkloadSetsToTake);
                if (versions is null)
                {
                    return 0;
                }

                if (!versions.Any())
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadVersionWithSpecifiedManifestNotFound, string.Join(' ', _workloadVersion)));
                }
                else if (_workloadSetOutputFormat?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
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
                var workloadSet = _installer.GetWorkloadSetContents(_workloadVersion.Single());
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

        private List<string> GetVersions(int numberOfWorkloadSetsToTake)
        {
            return GetVersions(numberOfWorkloadSetsToTake, new SdkFeatureBand(_sdkVersion), _installer, _includePreviews, PackageDownloader, _resolver);
        }

        private static List<string> GetVersions(int numberOfWorkloadSetsToTake, SdkFeatureBand featureBand, IInstaller installer, bool includePreviews, INuGetPackageDownloader packageDownloader, IWorkloadResolver resolver)
        {
            installer ??= GenerateInstaller(Cli.Utils.Reporter.NullReporter, featureBand, resolver, VerbosityOptions.d, interactive: false);
            var packageId = installer.GetManifestPackageId(new ManifestId("Microsoft.NET.Workloads"), featureBand);

            return packageDownloader.GetLatestPackageVersions(packageId, numberOfWorkloadSetsToTake, packageSourceLocation: null, includePreview: includePreviews)
                .GetAwaiter().GetResult()
                .Select(version => WorkloadSetVersion.FromWorkloadSetPackageVersion(featureBand, version.ToString()))
                .ToList();
        }

        private IEnumerable<string> FindBestWorkloadSetsFromComponents()
        {
            return FindBestWorkloadSetsFromComponents(new SdkFeatureBand(_sdkVersion), _installer, _includePreviews, PackageDownloader, _workloadVersion, _resolver, _numberOfWorkloadSetsToTake);
        }

        public static IEnumerable<string> FindBestWorkloadSetsFromComponents(SdkFeatureBand featureBand, IInstaller installer, bool includePreviews, INuGetPackageDownloader packageDownloader, IEnumerable<string> workloadVersions, IWorkloadResolver resolver, int numberOfWorkloadSetsToTake)
        {
            installer ??= GenerateInstaller(Cli.Utils.Reporter.NullReporter, featureBand, resolver, VerbosityOptions.d, interactive: false);
            List<string> versions;
            try
            {
                // 0 indicates 'give all versions'. Not all will match, so we don't know how many we will need
                versions = GetVersions(0, featureBand, installer, includePreviews, packageDownloader, resolver);
            }
            catch (NuGetPackageNotFoundException)
            {
                Cli.Utils.Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoWorkloadVersionsFound, featureBand));
                return null;
            }

            var manifestIdsAndVersions = workloadVersions.Select(version =>
            {
                var split = version.Split('@');
                return (new ManifestId(resolver.GetManifestFromWorkload(new WorkloadId(split[0])).Id), new ManifestVersion(split[1]));
            });

            // Since these are ordered by version (descending), the first is the highest version
            return versions.Where(version =>
            {
                var manifestVersions = installer.GetWorkloadSetContents(version).ManifestVersions;
                return manifestIdsAndVersions.All(tuple => manifestVersions.ContainsKey(tuple.Item1) && manifestVersions[tuple.Item1].Version.Equals(tuple.Item2));
            }).Take(numberOfWorkloadSetsToTake);
        }
    }
}
