// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
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


namespace Microsoft.DotNet.Cli.ToolPackage
{
    internal class ToolPackageDownloader
    {
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        protected readonly DirectoryPath _tempPackagesDir;
        protected readonly DirectoryPath _tempStageDir;
        private readonly IToolPackageStore _toolPackageStore;

        public ToolPackageDownloader(
            IToolPackageStore store,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string tempDirPath = null
        ){
            _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store));
            _tempPackagesDir = new DirectoryPath(tempDirPath ?? PathUtilities.CreateTempSubdirectory());
            _tempStageDir = _toolPackageStore.GetRandomStagingDirectory();
            _nugetPackageDownloader = nugetPackageDownloader ??
                                     new NuGetPackageDownloader.NuGetPackageDownloader(_tempStageDir);
        }

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

        public async Task<IToolPackage> InstallPackageAsync(PackageLocation packageLocation, PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null
            )
        { 
            var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, null, packageLocation.AdditionalFeeds);
            var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(packageId, null, packageSourceLocation);

            var tempExtractionDir = Path.GetDirectoryName(packagePath);
            Directory.CreateDirectory(_tempStageDir.Value);

            // Look fo nuget package on disk and read the version
            NuGetVersion version;

            using (FileStream packageStream = File.OpenRead(packagePath))
            {
                PackageArchiveReader reader = new PackageArchiveReader(packageStream);
                version = new NuspecReader(reader.GetNuspec()).GetVersion();
                
                var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
                var hashPath = new VersionFolderPathResolver(_tempStageDir.Value).GetHashPath(packageId.ToString(), version);

                Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
                File.WriteAllText(hashPath, packageHash);
            }

            var packageDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
            var tempToolExtractionDir = Path.Combine(tempExtractionDir, packageId.ToString(), version.ToString());
            var nupkgDir = Path.Combine(tempExtractionDir, packageId.ToString(), version.ToString());
            var filesInPackage = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempExtractionDir));

            if (Directory.Exists(packagePath))
            {
                throw new ToolPackageException(
                    string.Format(
                        CommonLocalizableStrings.ToolPackageConflictPackageId,
                        packageId,
                        version.ToNormalizedString()));
            }

            // To get runtimeGraph:
            var runtimeJsonPath = "C:\\Program Files\\dotnet\\sdk\\7.0.200\\RuntimeIdentifierGraph.json";
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
            NuGetv3LocalRepository localRepository = new(_tempStageDir.Value);
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
            new LockFileFormat().Write(Path.Combine(_tempStageDir.Value, "project.assets.json"), lockFile);

            var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
            Directory.CreateDirectory(packageRootDirectory.Value);

            FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(_tempStageDir.Value, packageDirectory.Value));

            return new ToolPackageInstance(id: packageId,
                            version: version,
                            packageDirectory: packageDirectory,
                            assetsJsonParentDirectory: packageDirectory);
        }
    }
}
