// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
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
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static System.Formats.Asn1.AsnWriter;


namespace Microsoft.DotNet.Cli.ToolPackage
{
    internal class ToolPackageDownloader
    {
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        protected readonly DirectoryPath _tempPackagesDir;
        protected readonly DirectoryPath _storePackagesDir;

        public ToolPackageDownloader(
            INuGetPackageDownloader nugetPackageDownloader = null,
        string tempDirPath = null
        )
        {
            _tempPackagesDir = new DirectoryPath(tempDirPath ?? PathUtilities.CreateTempSubdirectory());

            _storePackagesDir = new DirectoryPath ("C:\\Users\\liannie\\.dotnet\\tools\\.store");
            _nugetPackageDownloader = nugetPackageDownloader ??
                                     new NuGetPackageDownloader.NuGetPackageDownloader(_storePackagesDir);
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
            string verbosity = null)
        { 
            var tempDirsToDelete = new List<string>();
            var tempFilesToDelete = new List<string>();

            //var versionFromVersionRange = versionRange?.ToString("N", new VersionRangeFormatter()) ?? "*";
            Console.WriteLine(versionRange);
            //Console.WriteLine(versionFromVersionRange);
            var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, null, packageLocation.AdditionalFeeds);
            var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(packageId, null, packageSourceLocation);
            tempFilesToDelete.Add(packagePath);

            var tempExtractionDir = Path.GetDirectoryName(packagePath);
            

            // Look fo nuget package on disk and read the version 
            await using FileStream packageStream = File.OpenRead(packagePath);
            PackageArchiveReader reader = new PackageArchiveReader(packageStream);
            var version = new NuspecReader(reader.GetNuspec()).GetVersion();

            

            var tempToolExtractionDir = Path.Combine(tempExtractionDir, packageId.ToString(), version.ToString(), "tools");
            var nupkgDir = Path.Combine(tempExtractionDir, packageId.ToString(), version.ToString());

            tempDirsToDelete.Add(tempExtractionDir);
            
            var filesInPackage = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempToolExtractionDir));

            var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
            var hashPath = new VersionFolderPathResolver(tempExtractionDir).GetHashPath(packageId.ToString(), version);
            File.WriteAllText(hashPath, packageHash);

            //Copy nupkg file to hash file folder
            File.Copy(packagePath, Path.Combine(nupkgDir, Path.GetFileName(packagePath)));

            if (Directory.Exists(packagePath))
            {
                throw new ToolPackageException(
                    string.Format(
                        CommonLocalizableStrings.ToolPackageConflictPackageId,
                        packageId,
                        version.ToNormalizedString()));
            }

            // create asset files

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
            NuGetv3LocalRepository localRepository = new(tempExtractionDir);
            Console.WriteLine(tempExtractionDir);
            var package = localRepository.FindPackage(packageId.ToString(), version);

            var collection = new ContentItemCollection();
            collection.Load(package.Files);

            //  Create criteria
            var managedCriteria = new List<SelectionCriteria>(1);

            var currentTargetFramework = NuGetFramework.Parse("net8.0");

            //  Not supporting FallbackFramework for tools
            var standardCriteria = conventions.Criteria.ForFrameworkAndRuntime(
                //NuGetFramework.Parse(targetFramework),
                currentTargetFramework,
                RuntimeInformation.RuntimeIdentifier);
            managedCriteria.Add(standardCriteria);

            //  To be implemented
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
            new LockFileFormat().Write(Path.Combine(tempExtractionDir, "project.assets.json"), lockFile);

            return new ToolPackageInstance(id: packageId,
                            version: version,
                            packageDirectory: new DirectoryPath(tempExtractionDir),
                            assetsJsonParentDirectory: new DirectoryPath(tempExtractionDir));
            //cleanup
            /*foreach (var dir in tempDirsToDelete)
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }*/

        }
    }
}
