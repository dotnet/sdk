// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.ToolPackage
{
    internal class ToolPackageDownloader : IToolPackageDownloader
    {
        private INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IToolPackageStore _toolPackageStore;

        // The directory that the tool will be downloaded to
        protected DirectoryPath _toolDownloadDir;

        // The directory that the tool package is returned 
        protected DirectoryPath _toolReturnPackageDirectory;

        // The directory that the tool asset file is returned
        protected DirectoryPath _toolReturnJsonParentDirectory;

        // The directory that global tools first downloaded
        // example: C:\Users\username\.dotnet\tools\.store\.stage\tempFolder
        protected readonly DirectoryPath _globalToolStageDir;

        // The directory that local tools first downloaded
        // example: C:\Users\username\.nuget\package
        protected readonly DirectoryPath _localToolDownloadDir;

        // The directory that local tools' asset files located
        // example: C:\Users\username\AppData\Local\Temp\tempFolder
        protected readonly DirectoryPath _localToolAssetDir;

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption;

        protected readonly string _runtimeJsonPath;

        public ToolPackageDownloader(
            IToolPackageStore store,
            string runtimeJsonPathForTests = null
        )
        {
            _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store)); ;
            _globalToolStageDir = _toolPackageStore.GetRandomStagingDirectory();
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            _localToolDownloadDir = new DirectoryPath(SettingsUtility.GetGlobalPackagesFolder(settings));
            
            _localToolAssetDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
            _runtimeJsonPath = runtimeJsonPathForTests ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RuntimeIdentifierGraph.json");
        }

        public IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null,
            bool isGlobalTool = false
            )
        {
            var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
            string rollbackDirectory = null;

            return TransactionalAction.Run<IToolPackage>(
                action: () =>
                {
                    ILogger nugetLogger = new NullLogger();
                    if (verbosity != null && (verbosity == VerbosityOptions.d.ToString() ||
                                              verbosity == VerbosityOptions.detailed.ToString() ||
                                              verbosity == VerbosityOptions.diag.ToString() ||
                                              verbosity == VerbosityOptions.diagnostic.ToString()))
                    {
                        nugetLogger = new NuGetConsoleLogger();
                    }
                    _toolDownloadDir = isGlobalTool ? _globalToolStageDir : _localToolDownloadDir;
                    var assetFileDirectory = isGlobalTool ? _globalToolStageDir : _localToolAssetDir;
                    _nugetPackageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(_toolDownloadDir, verboseLogger: nugetLogger, isNuGetTool: true);
                    rollbackDirectory = _toolDownloadDir.Value;

                    NuGetVersion version = DownloadAndExtractPackage(packageLocation, packageId, _nugetPackageDownloader, _toolDownloadDir.Value, _toolPackageStore).GetAwaiter().GetResult();
                    CreateAssetFiles(packageId, version, _toolDownloadDir, assetFileDirectory, _runtimeJsonPath);

                    if (isGlobalTool)
                    {
                        _toolReturnPackageDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
                        _toolReturnJsonParentDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
                        var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
                        Directory.CreateDirectory(packageRootDirectory.Value);
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(_globalToolStageDir.Value, _toolReturnPackageDirectory.Value));
                        rollbackDirectory = _toolReturnPackageDirectory.Value;
                    }
                    else
                    {
                        _toolReturnPackageDirectory = _toolDownloadDir;
                        _toolReturnJsonParentDirectory = _localToolAssetDir;
                    }

                    return new ToolPackageInstance(id: packageId,
                                    version: version,
                                    packageDirectory: _toolReturnPackageDirectory,
                                    assetsJsonParentDirectory: _toolReturnJsonParentDirectory);
                },
                rollback: () =>
                {
                    if (rollbackDirectory != null && Directory.Exists(rollbackDirectory))
                    {
                        Directory.Delete(rollbackDirectory, true);
                    }
                    // Delete the root if it is empty
                    if (Directory.Exists(packageRootDirectory.Value) &&
                        !Directory.EnumerateFileSystemEntries(packageRootDirectory.Value).Any())
                    {
                        Directory.Delete(packageRootDirectory.Value, false);
                    }
                });
        }

        // The following methods are copied from the LockFileUtils class in Nuget.Client
        private static void AddToolsAssets(
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

        private static async Task<NuGetVersion> DownloadAndExtractPackage(
            PackageLocation packageLocation,
            PackageId packageId,
            INuGetPackageDownloader _nugetPackageDownloader,
            string hashPathLocation,
            IToolPackageStore toolPackageStore
            )
        {
            var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, null, packageLocation.AdditionalFeeds);
            var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(packageId, null, packageSourceLocation);

            // look for package on disk and read the version
            NuGetVersion version;

            using (FileStream packageStream = File.OpenRead(packagePath))
            {
                PackageArchiveReader reader = new PackageArchiveReader(packageStream);
                version = new NuspecReader(reader.GetNuspec()).GetVersion();

                var packageDirectory = toolPackageStore.GetPackageDirectory(packageId, version);

                if (Directory.Exists(packageDirectory.Value))
                {
                    throw new ToolPackageException(
                        string.Format(
                            CommonLocalizableStrings.ToolPackageConflictPackageId,
                            packageId,
                            version.ToNormalizedString()));
                }

                if (Directory.Exists(packagePath))
                {
                    throw new ToolPackageException(
                        string.Format(
                            CommonLocalizableStrings.ToolPackageConflictPackageId,
                            packageId,
                            version.ToNormalizedString()));
                }

                var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
                var hashPath = new VersionFolderPathResolver(hashPathLocation).GetHashPath(packageId.ToString(), version);

                Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
                File.WriteAllText(hashPath, packageHash);
            }

            // Extract the package
            var nupkgDir = Path.Combine(hashPathLocation, packageId.ToString(), version.ToString());
            var filesInPackage = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(nupkgDir));

            return version;
        }

        private static void CreateAssetFiles(
            PackageId packageId,
            NuGetVersion version,
            DirectoryPath nugetLocalRepository,
            DirectoryPath assetFileDirectory,
            string runtimeJsonGraph)
        {
            // To get runtimeGraph:
            var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeJsonGraph);

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
            // Use major.minor version of currently running version of .NET
            var currentTargetFramework = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version(Environment.Version.Major, Environment.Version.Minor));

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
        }
    }
}
