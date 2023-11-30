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
using Microsoft.TemplateEngine.Utils;
using System.Text.Json;
using System.Xml;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.ToolPackage
{
    internal class ToolPackageDownloader : IToolPackageDownloader
    {
        private readonly IToolPackageStore _toolPackageStore;

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

        protected readonly string _runtimeJsonPath;

        public ToolPackageDownloader(
            IToolPackageStore store,
            string runtimeJsonPathForTests = null
        )
        {
            _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store));
            _globalToolStageDir = _toolPackageStore.GetRandomStagingDirectory();
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            _localToolDownloadDir = new DirectoryPath(SettingsUtility.GetGlobalPackagesFolder(settings));
            
            _localToolAssetDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
            _runtimeJsonPath = runtimeJsonPathForTests ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RuntimeIdentifierGraph.json");
        }

        public IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            VersionRange versionRange = null,
            string targetFramework = null,
            bool isGlobalTool = false,
            bool isGlobalToolRollForward = false
            )
        {
            var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
            string rollbackDirectory = null;

            return TransactionalAction.Run<IToolPackage>(
                action: () =>
                {
                    ILogger nugetLogger = new NullLogger();
                    if (verbosity.IsDetailedOrDiagnostic())
                    {
                        nugetLogger = new NuGetConsoleLogger();
                    }

                    if (versionRange == null)
                    {
                        var versionString = "*";
                        versionRange = VersionRange.Parse(versionString);
                    }

                    var toolDownloadDir = isGlobalTool ? _globalToolStageDir : _localToolDownloadDir;
                    var assetFileDirectory = isGlobalTool ? _globalToolStageDir : _localToolAssetDir;
                    var nugetPackageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(toolDownloadDir, verboseLogger: nugetLogger, isNuGetTool: true);

                    var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, null, packageLocation.AdditionalFeeds);

                    bool givenSpecificVersion = false;
                    if (versionRange.MinVersion != null && versionRange.MaxVersion != null && versionRange.MinVersion == versionRange.MaxVersion)
                    {
                        givenSpecificVersion = true;
                    }
                    NuGetVersion packageVersion = nugetPackageDownloader.GetBestPackageVersionAsync(packageId, versionRange, packageSourceLocation).GetAwaiter().GetResult();

                    rollbackDirectory = isGlobalTool ? toolDownloadDir.Value: Path.Combine(toolDownloadDir.Value, packageId.ToString(), packageVersion.ToString());

                    if (isGlobalTool)
                    {
                        NuGetv3LocalRepository nugetPackageRootDirectory = new(Path.Combine(_toolPackageStore.GetRootPackageDirectory(packageId).ToString().Trim('"'), packageVersion.ToString()));
                        var globalPackage = nugetPackageRootDirectory.FindPackage(packageId.ToString(), packageVersion);

                        if (globalPackage != null)
                        {
                            throw new ToolPackageException(
                                string.Format(
                                    CommonLocalizableStrings.ToolPackageConflictPackageId,
                                    packageId,
                                    packageVersion.ToNormalizedString()));
                        }
                    }
                    NuGetv3LocalRepository localRepository = new(toolDownloadDir.Value);
                    var package = localRepository.FindPackage(packageId.ToString(), packageVersion);

                    if (package == null)
                    {
                        DownloadAndExtractPackage(packageLocation, packageId, nugetPackageDownloader, toolDownloadDir.Value, _toolPackageStore, packageVersion, packageSourceLocation, includeUnlisted: givenSpecificVersion).GetAwaiter().GetResult();
                    }
                    else if(isGlobalTool)
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CommonLocalizableStrings.ToolPackageConflictPackageId,
                                packageId,
                                packageVersion.ToNormalizedString()));
                    }
                                       
                    CreateAssetFile(packageId, packageVersion, toolDownloadDir, assetFileDirectory, _runtimeJsonPath, targetFramework);

                    if (isGlobalToolRollForward)
                    {
                        UpdateRuntimeConfig(packageId, packageVersion, toolDownloadDir);
                    }
                    
                    DirectoryPath toolReturnPackageDirectory;
                    DirectoryPath toolReturnJsonParentDirectory;

                    if (isGlobalTool)
                    {
                        toolReturnPackageDirectory = _toolPackageStore.GetPackageDirectory(packageId, packageVersion);
                        toolReturnJsonParentDirectory = _toolPackageStore.GetPackageDirectory(packageId, packageVersion);
                        var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
                        Directory.CreateDirectory(packageRootDirectory.Value);
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(_globalToolStageDir.Value, toolReturnPackageDirectory.Value));
                        rollbackDirectory = toolReturnPackageDirectory.Value;
                    }
                    else
                    {
                        toolReturnPackageDirectory = toolDownloadDir;
                        toolReturnJsonParentDirectory = _localToolAssetDir;
                    }

                    return new ToolPackageInstance(id: packageId,
                                    version: packageVersion,
                                    packageDirectory: toolReturnPackageDirectory,
                                    assetsJsonParentDirectory: toolReturnJsonParentDirectory);
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

        private static void UpdateRuntimeConfig(
            PackageId packageId,
            NuGetVersion packageVersion,
            DirectoryPath toolDownloadDir
            )
        {
            var toolsDirectory = Path.Combine(toolDownloadDir.ToString().Trim('"'), packageId.ToString(), packageVersion.ToString(), "tools");

            // TBD: if roll forward all frameworks
            string[] subDirectories = Directory.GetDirectories(toolsDirectory);
            foreach (string subDirectory in subDirectories)
            {
                UpdateRuntimeConfigFile(subDirectory, packageId);
            }
        }

        private static void UpdateRuntimeConfigFile(
            string frameworkDir,
            PackageId packageId
            )
        {
            // Get the setting file
            var settingFilePath = Path.Combine(frameworkDir, "any", "DotnetToolSettings.xml");
            if (!File.Exists(settingFilePath))
            {
                throw new ToolPackageException(
                    string.Format(
                        CommonLocalizableStrings.ToolSettingsNotFound,
                        packageId
                        ));
            }

            // Get runtimeConfig file
            string runtimeConfigFileFolder = null;
            XDocument dotnetToolSettings = XDocument.Load(settingFilePath);
            XElement commandElement = dotnetToolSettings.Descendants("Command").FirstOrDefault();
            if (commandElement != null)
            {
                XAttribute entryPointAttribute = commandElement.Attribute("EntryPoint");
                if (entryPointAttribute != null)
                {
                    runtimeConfigFileFolder = entryPointAttribute.Value;
                }
            }

            var runtimeConfigFilePath = Path.Combine(Path.GetDirectoryName(settingFilePath), $"{Path.GetFileNameWithoutExtension(runtimeConfigFileFolder)}{".runtimeconfig.json"}");

            // Update the runtimeconfig.json file
            string existingJson = File.ReadAllText(runtimeConfigFilePath);

            var jsonObject = JObject.Parse(existingJson);
            var runtimeOptions = jsonObject["runtimeOptions"] as JObject;
            if (runtimeOptions != null)
            {
                runtimeOptions["rollForward"] = "LatestMajor";
                string updateJson = jsonObject.ToString();
                File.WriteAllText(runtimeConfigFilePath, updateJson);
            }
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
            INuGetPackageDownloader nugetPackageDownloader,
            string packagesRootPath,
            IToolPackageStore toolPackageStore,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation,
            bool includeUnlisted = false
            )
        {
            var packagePath = await nugetPackageDownloader.DownloadPackageAsync(packageId, packageVersion, packageSourceLocation, includeUnlisted: includeUnlisted).ConfigureAwait(false);

            // look for package on disk and read the version
            NuGetVersion version;

            using (FileStream packageStream = File.OpenRead(packagePath))
            {
                PackageArchiveReader reader = new PackageArchiveReader(packageStream);
                version = new NuspecReader(reader.GetNuspec()).GetVersion();

                var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
                var hashPath = new VersionFolderPathResolver(packagesRootPath).GetHashPath(packageId.ToString(), version);

                Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
                File.WriteAllText(hashPath, packageHash);
            }

            // Extract the package
            var nupkgDir = Path.Combine(packagesRootPath, packageId.ToString(), version.ToString());
            var filesInPackage = await nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(nupkgDir));

            return version;
        }

        private static void CreateAssetFile(
            PackageId packageId,
            NuGetVersion version,
            DirectoryPath packagesRootPath,
            DirectoryPath assetFileDirectory,
            string runtimeJsonGraph,
            string targetFramework = null
            )
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
            NuGetv3LocalRepository localRepository = new(packagesRootPath.Value);
            var package = localRepository.FindPackage(packageId.ToString(), version);

            var collection = new ContentItemCollection();
            collection.Load(package.Files);

            //  Create criteria
            var managedCriteria = new List<SelectionCriteria>(1);
            // Use major.minor version of currently running version of .NET
            NuGetFramework currentTargetFramework;
            if(targetFramework != null)
            {
                currentTargetFramework = NuGetFramework.Parse(targetFramework); 
            }
            else
            {
                currentTargetFramework = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version(Environment.Version.Major, Environment.Version.Minor));
            }

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
