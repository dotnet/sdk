// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageDownloaderMock : IToolPackageDownloader
    {
        private readonly IToolPackageStore _toolPackageStore;

        protected DirectoryPath _toolDownloadDir;
        protected DirectoryPath _toolReturnPackageDirectory;
        protected DirectoryPath _toolReturnJsonParentDirectory;

        protected readonly DirectoryPath _globalToolStageDir;
        protected readonly DirectoryPath _localToolDownloadDir;
        protected readonly DirectoryPath _localToolAssetDir;

        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string DefaultToolCommandName = "SimulatorCommand";
        public const string DefaultPackageName = "global.tool.console.demo";
        public const string DefaultPackageVersion = "1.0.4";
        public const string FakeCommandSettingsFileName = "FakeDotnetToolSettings.json";


        private const string ProjectFileName = "TempProject.csproj";
        private readonly IFileSystem _fileSystem;
        private readonly IReporter _reporter;
        private readonly List<MockFeed> _feeds;

        private readonly Dictionary<PackageId, IEnumerable<string>> _warningsMap;
        private readonly Dictionary<PackageId, IReadOnlyList<FilePath>> _packagedShimsMap;
        private readonly Dictionary<PackageId, IEnumerable<NuGetFramework>> _frameworksMap;
        private readonly Action _downloadCallback;

        public ToolPackageDownloaderMock(
            IToolPackageStore store,
            IFileSystem fileSystem,
            IReporter reporter = null,
            List<MockFeed> feeds = null,
            Action downloadCallback = null,
            Dictionary<PackageId, IEnumerable<string>> warningsMap = null,
            Dictionary<PackageId, IReadOnlyList<FilePath>> packagedShimsMap = null,
            Dictionary<PackageId, IEnumerable<NuGetFramework>> frameworksMap = null
        )
        {
            _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store)); ;
            _globalToolStageDir = _toolPackageStore.GetRandomStagingDirectory();
            _localToolDownloadDir = new DirectoryPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "nuget", "package"));
            _localToolAssetDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());

            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _reporter = reporter;

            _warningsMap = warningsMap ?? new Dictionary<PackageId, IEnumerable<string>>();
            _packagedShimsMap = packagedShimsMap ?? new Dictionary<PackageId, IReadOnlyList<FilePath>>();
            _frameworksMap = frameworksMap ?? new Dictionary<PackageId, IEnumerable<NuGetFramework>>();
            _downloadCallback = downloadCallback;

            if (feeds == null)
            {
                _feeds = new List<MockFeed>();
                _feeds.Add(new MockFeed
                {
                    Type = MockFeedType.FeedFromGlobalNugetConfig,
                    Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = DefaultPackageName,
                                Version = DefaultPackageVersion,
                                ToolCommandName = DefaultToolCommandName,
                            }
                        }
                });
            }
            else
            {
                _feeds = feeds;
            }
        }

        

        public IToolPackage InstallPackageAsync(PackageLocation packageLocation, PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null,
            bool isGlobalTool = false
            )
        {
            string rollbackDirectory = null;
            var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);

            return TransactionalAction.Run<IToolPackage>(
                action: () =>
                {
                    var versionString = versionRange?.OriginalString ?? "*";
                    versionRange = VersionRange.Parse(versionString);
                    _toolDownloadDir = isGlobalTool ? _globalToolStageDir : _localToolDownloadDir;
                    var assetFileDirectory = isGlobalTool ? _globalToolStageDir : _localToolAssetDir;
                    rollbackDirectory = _toolDownloadDir.Value;
                    // _nugetPackageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(_toolDownloadDir);

                    // NuGetVersion version = DownloadAndExtractPackage(packageLocation, packageId, _nugetPackageDownloader, _toolDownloadDir.Value).GetAwaiter().GetResult();

                    if (string.IsNullOrEmpty(packageId.ToString()))
                    {
                        throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
                    }

                    var feedPackage = GetPackage(
                        packageId.ToString(),
                        versionRange,
                        packageLocation.NugetConfig,
                        packageLocation.RootConfigDirectory);

                    var packageVersion = feedPackage.Version;
                    targetFramework = string.IsNullOrEmpty(targetFramework) ? "targetFramework" : targetFramework;

                    var fakeExecutableSubDirectory = Path.Combine(
                       packageId.ToString().ToLowerInvariant(),
                       packageVersion.ToLowerInvariant(),
                       "tools",
                       targetFramework,
                       Constants.AnyRid);
                    var fakeExecutablePath = Path.Combine(fakeExecutableSubDirectory, FakeEntrypointName);

                    _fileSystem.Directory.CreateDirectory(Path.Combine(_toolDownloadDir.Value, fakeExecutableSubDirectory));
                    _fileSystem.File.CreateEmptyFile(Path.Combine(_toolDownloadDir.Value, fakeExecutablePath));
                    _fileSystem.File.WriteAllText(
                        _toolDownloadDir.WithFile("project.assets.json").Value,
                        fakeExecutablePath);
                    _fileSystem.File.WriteAllText(
                        _toolDownloadDir.WithFile(FakeCommandSettingsFileName).Value,
                        JsonSerializer.Serialize(new { Name = feedPackage.ToolCommandName }));

                    if (_downloadCallback != null)
                    {
                        _downloadCallback();
                    }

                    var version = _toolPackageStore.GetStagedPackageVersion(_toolDownloadDir, packageId);
                    var packageDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
                    if (_fileSystem.Directory.Exists(packageDirectory.Value))
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CommonLocalizableStrings.ToolPackageConflictPackageId,
                                packageId,
                                version.ToNormalizedString()));
                    }



                    // CreateAssetFiles(packageId, version, _toolDownloadDir, assetFileDirectory);
                    if (!isGlobalTool)
                    {
                        packageDirectory = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation()).WithSubDirectories(packageId.ToString());
                        _fileSystem.Directory.CreateDirectory(packageDirectory.Value);
                        var executable = packageDirectory.WithFile("exe");
                        _fileSystem.File.CreateEmptyFile(executable.Value); 
                        return new TestToolPackage
                        {
                            Id = packageId,
                            Version = NuGetVersion.Parse(feedPackage.Version),
                            Commands = new List<RestoredCommand> {
                            new RestoredCommand(new ToolCommandName(feedPackage.ToolCommandName), "runner", executable) },
                            Warnings = Array.Empty<string>(),
                            PackagedShims = Array.Empty<FilePath>()
                        };
                    }
                    if (isGlobalTool)
                    {
                        _toolReturnPackageDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
                        _toolReturnJsonParentDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
                        var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
                        // Directory.CreateDirectory(packageRootDirectory.Value);
                        // FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(_globalToolStageDir.Value, _toolReturnPackageDirectory.Value));
                        _fileSystem.Directory.CreateDirectory(packageRootDirectory.Value);
                        _fileSystem.Directory.Move(_toolDownloadDir.Value, packageDirectory.Value);
                        rollbackDirectory = packageDirectory.Value;
                    }
                    else
                    {
                        _toolReturnPackageDirectory = _toolDownloadDir;
                        _toolReturnJsonParentDirectory = _localToolAssetDir;
                    }

                    IEnumerable<string> warnings = null;
                    _warningsMap.TryGetValue(packageId, out warnings);

                    IReadOnlyList<FilePath> packedShims = null;
                    _packagedShimsMap.TryGetValue(packageId, out packedShims);

                    IEnumerable<NuGetFramework> frameworks = null;
                    _frameworksMap.TryGetValue(packageId, out frameworks);

                    return new ToolPackageMock(_fileSystem, id: packageId,
                                    version: version,
                                    packageDirectory: packageDirectory,
                                    warnings: warnings, packagedShims: packedShims, frameworks: frameworks);
                },
                rollback: () =>
                {
                    if (rollbackDirectory != null && _fileSystem.Directory.Exists(rollbackDirectory))
                    {
                        _fileSystem.Directory.Delete(rollbackDirectory, true);
                    }
                    if (_fileSystem.Directory.Exists(packageRootDirectory.Value) &&
                    !_fileSystem.Directory.EnumerateFileSystemEntries(packageRootDirectory.Value).Any())
                    {
                        _fileSystem.Directory.Delete(packageRootDirectory.Value, false);
                    }
                    
                });
        }

        /*private static void AddToolsAssets(
            ManagedCodeConventions managedCodeConventions,
            LockFileTargetLibrary lockFileLib,
            ContentItemCollection contentItems,
            IReadOnlyList<SelectionCriteria> orderedCriteria)
        {
            var toolsGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                managedCodeConventions.Patterns.ToolsAssemblies);

            lockFileLib.ToolsAssemblies.AddRange(toolsGroup);
        }

        private static IEnumerable<LockFileItem> GetLockFileItems(
            IReadOnlyList<SelectionCriteria> criteria,
            ContentItemCollection items,
            params PatternSet[] patterns)
        {
            return GetLockFileItems(criteria, items, additionalAction: null, patterns);
        }

        private static IEnumerable<LockFileItem> GetLockFileItems(
               IReadOnlyList<SelectionCriteria> criteria,
               ContentItemCollection items,
               Action<LockFileItem> additionalAction,
               params PatternSet[] patterns)
        {
            // Loop through each criteria taking the first one that matches one or more items.
            foreach (var managedCriteria in criteria)
            {
                var group = items.FindBestItemGroup(
                    managedCriteria,
                    patterns);

                if (group != null)
                {
                    foreach (var item in group.Items)
                    {
                        var newItem = new LockFileItem(item.Path);
                        object locale;
                        if (item.Properties.TryGetValue("locale", out locale))
                        {
                            newItem.Properties["locale"] = (string)locale;
                        }
                        object related;
                        if (item.Properties.TryGetValue("related", out related))
                        {
                            newItem.Properties["related"] = (string)related;
                        }
                        additionalAction?.Invoke(newItem);
                        yield return newItem;
                    }
                    // Take only the first group that has items
                    break;
                }
            }

            yield break;
        }

        private static void CreateAssetFiles(
            PackageId packageId,
            NuGetVersion version,
            DirectoryPath nugetLocalRepository,
            DirectoryPath assetFileDirectory)
        {
            // To get runtimeGraph:
            var runtimeJsonPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "runtimeIdentifierGraph.json");
            var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeJsonPath);

            // Create ManagedCodeConventions:
            var conventions = new ManagedCodeConventions(runtimeGraph);

            //  Create LockFileTargetLibrary
            var lockFileLib = new LockFileTargetLibrary()
            {
                Name = packageId.ToString(),
                Version = version,
                Type = LibraryType.Package,
                PackageType = new List<PackageType>() { PackageType.DotnetTool }
            };

            //  Create NuGetv3LocalRepository
            NuGetv3LocalRepository localRepository = new(nugetLocalRepository.Value);
            var package = localRepository.FindPackage(packageId.ToString(), version);

            var collection = new ContentItemCollection();
            collection.Load(package.Files);

            //  Create criteria
            var managedCriteria = new List<SelectionCriteria>(1);
            var currentTargetFramework = NuGetFramework.Parse("net8.0");

            var standardCriteria = conventions.Criteria.ForFrameworkAndRuntime(
                currentTargetFramework,
                RuntimeInformation.RuntimeIdentifier);
            managedCriteria.Add(standardCriteria);

            //  Create asset file
            if (lockFileLib.PackageType.Contains(PackageType.DotnetTool))
            {
                AddToolsAssets(conventions, lockFileLib, collection, managedCriteria);
            }

            var lockFile = new LockFile();
            var lockFileTarget = new LockFileTarget()
            {
                TargetFramework = currentTargetFramework,
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
            };
            lockFileTarget.Libraries.Add(lockFileLib);
            lockFile.Targets.Add(lockFileTarget);
            new LockFileFormat().Write(Path.Combine(assetFileDirectory.Value, "project.assets.json"), lockFile);
        }*/

        public MockFeedPackage GetPackage(
            string packageId,
            VersionRange versionRange,
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null)
        {
            var allPackages = _feeds
                .Where(feed =>
                {
                    if (nugetConfig == null)
                    {
                        return SimulateNugetSearchNugetConfigAndMatch(
                            rootConfigDirectory,
                            feed);
                    }
                    else
                    {
                        return ExcludeOtherFeeds(nugetConfig.Value, feed);
                    }
                })
                .SelectMany(f => f.Packages)
                .Where(f => f.PackageId == packageId)
                .ToList();

            var bestVersion = versionRange.FindBestMatch(allPackages.Select(p => NuGetVersion.Parse(p.Version)));

            var package = allPackages.FirstOrDefault(p => NuGetVersion.Parse(p.Version).Equals(bestVersion));

            if (package == null)
            {
                _reporter?.WriteLine($"Error: failed to restore package {packageId}.");
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }

            return package;
        }

        /// <summary>
        /// Simulate NuGet search nuget config from parent directories.
        /// Assume all nuget.config has Clear
        /// And then filter against mock feed
        /// </summary>
        private bool SimulateNugetSearchNugetConfigAndMatch(
            DirectoryPath? rootConfigDirectory,
            MockFeed feed)
        {
            if (rootConfigDirectory != null)
            {
                var probedNugetConfig = EnumerateDefaultAllPossibleNuGetConfig(rootConfigDirectory.Value)
                    .FirstOrDefault(possibleNugetConfig =>
                        _fileSystem.File.Exists(possibleNugetConfig.Value));

                if (!Equals(probedNugetConfig, default(FilePath)))
                {
                    return (feed.Type == MockFeedType.FeedFromLookUpNugetConfig) ||
                           (feed.Type == MockFeedType.ImplicitAdditionalFeed) ||
                           (feed.Type == MockFeedType.FeedFromLookUpNugetConfig
                            && feed.Uri == probedNugetConfig.Value);
                }
            }

            return feed.Type != MockFeedType.ExplicitNugetConfig
                    && feed.Type != MockFeedType.FeedFromLookUpNugetConfig;
        }

        private static IEnumerable<FilePath> EnumerateDefaultAllPossibleNuGetConfig(DirectoryPath probStart)
        {
            DirectoryPath? currentSearchDirectory = probStart;
            while (currentSearchDirectory.HasValue)
            {
                var tryNugetConfig = currentSearchDirectory.Value.WithFile("nuget.config");
                yield return tryNugetConfig;
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
        }

        private static bool ExcludeOtherFeeds(FilePath nugetConfig, MockFeed f)
        {
            return f.Type == MockFeedType.ImplicitAdditionalFeed
                   || (f.Type == MockFeedType.ExplicitNugetConfig && f.Uri == nugetConfig.Value);
        }

        private class TestToolPackage : IToolPackage
        {
            public PackageId Id { get; set; }

            public NuGetVersion Version { get; set; }
            public DirectoryPath PackageDirectory { get; set; }

            public IReadOnlyList<RestoredCommand> Commands { get; set; }

            public IEnumerable<string> Warnings { get; set; }

            public IReadOnlyList<FilePath> PackagedShims { get; set; }

            public IEnumerable<NuGetFramework> Frameworks => throw new NotImplementedException();
        }
    }
}
