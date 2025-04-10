// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.Reflection;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

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

    private readonly string _currentWorkingDirectory;

    public ToolPackageDownloader(
        IToolPackageStore store,
        string runtimeJsonPathForTests = null,
        string currentWorkingDirectory = null
    )
    {
        _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store));
        _globalToolStageDir = _toolPackageStore.GetRandomStagingDirectory();
        ISettings settings = Settings.LoadDefaultSettings(currentWorkingDirectory ?? Directory.GetCurrentDirectory());
        _localToolDownloadDir = new DirectoryPath(SettingsUtility.GetGlobalPackagesFolder(settings));
        _currentWorkingDirectory = currentWorkingDirectory;
        
        _localToolAssetDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
        _runtimeJsonPath = runtimeJsonPathForTests ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RuntimeIdentifierGraph.json");
    }

    public IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
        VerbosityOptions verbosity = VerbosityOptions.normal,
        VersionRange versionRange = null,
        string targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig restoreActionConfig = null
        )
    {
        var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
        

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

                var nugetPackageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(
                    toolDownloadDir,
                    verboseLogger: nugetLogger,
                    verifySignatures: verifySignatures,
                    shouldUsePackageSourceMapping: true,
                    restoreActionConfig: restoreActionConfig,
                    verbosityOptions: verbosity,
                    currentWorkingDirectory: _currentWorkingDirectory);

                var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, packageLocation.SourceFeedOverrides, packageLocation.AdditionalFeeds);

                bool givenSpecificVersion = false;
                if (versionRange.MinVersion != null && versionRange.MaxVersion != null && versionRange.MinVersion == versionRange.MaxVersion)
                {
                    givenSpecificVersion = true;
                }
                NuGetVersion packageVersion = nugetPackageDownloader.GetBestPackageVersionAsync(packageId, versionRange, packageSourceLocation).GetAwaiter().GetResult();

                

                if (isGlobalTool)
                {
                    NuGetv3LocalRepository nugetPackageRootDirectory = new(new VersionFolderPathResolver(_toolPackageStore.Root.Value).GetInstallPath(packageId.ToString(), packageVersion));
                    var globalPackage = nugetPackageRootDirectory.FindPackage(packageId.ToString(), packageVersion);

                    if (globalPackage != null)
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CliStrings.ToolPackageConflictPackageId,
                                packageId,
                                packageVersion.ToNormalizedString()));
                    }
                }
                NuGetv3LocalRepository localRepository = new(toolDownloadDir.Value);

                DirectoryPath toolReturnPackageDirectory;
                DirectoryPath toolReturnJsonParentDirectory;

                // Refactored from inner method to direct call
                (toolReturnPackageDirectory, toolReturnJsonParentDirectory) = DownloadAndMovePackage(
                    isGlobalTool,
                    toolDownloadDir,
                    packageId,
                    packageVersion,
                    localRepository,
                    nugetPackageDownloader,
                    packageSourceLocation,
                    givenSpecificVersion,
                    assetFileDirectory,
                    targetFramework,
                    packageRootDirectory,
                    _runtimeJsonPath,
                    _toolPackageStore,
                    _globalToolStageDir);

                var toolConfiguration = ToolPackageInstance.GetToolConfiguration(packageId, toolReturnPackageDirectory, toolReturnJsonParentDirectory);
                if (toolConfiguration.RidSpecificPackages?.Any() == true)
                {
                    var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(_runtimeJsonPath);
                    var bestRuntimeIdentifier = Microsoft.NET.Build.Tasks.NuGetUtils.GetBestMatchingRid(runtimeGraph, RuntimeInformation.RuntimeIdentifier, toolConfiguration.RidSpecificPackages.Keys, out bool wasInGraph);
                    if (bestRuntimeIdentifier == null)
                    {
                        //  TODO: Localize
                        throw new ToolPackageException($"The tool does not support the current architecture or operating system (Runtime Identifier {RuntimeInformation.RuntimeIdentifier}");
                    }

                    var resolvedPackage = toolConfiguration.RidSpecificPackages[bestRuntimeIdentifier];

                    var (resolvedPackageDirectory, resolvedAssetsJsonParentDirectory) = DownloadAndMovePackage(
                        isGlobalTool,
                        toolDownloadDir,
                        new PackageId(resolvedPackage.Id),
                        resolvedPackage.Version,
                        localRepository,
                        nugetPackageDownloader,
                        packageSourceLocation,
                        givenSpecificVersion: true, // Primary package manifest should always specify a specific version of RID-specific package
                        //  TODO: Probably can't use the same asset directory as the primary package
                        assetFileDirectory,
                        targetFramework,
                        packageRootDirectory,
                        _runtimeJsonPath,
                        _toolPackageStore,
                        _globalToolStageDir);
                }


                var toolPackageInstance = new ToolPackageInstance(id: packageId,
                                version: packageVersion,
                                packageDirectory: toolReturnPackageDirectory,
                                assetsJsonParentDirectory: toolReturnJsonParentDirectory);

                if (isGlobalToolRollForward)
                {
                    UpdateRuntimeConfig(toolPackageInstance);
                }

                return toolPackageInstance;
            });
    }

    private (DirectoryPath toolReturnPackageDirectory, DirectoryPath toolReturnJsonParentDirectory) DownloadAndMovePackage(
        bool isGlobalTool,
        DirectoryPath toolDownloadDir,
        PackageId packageId,
        NuGetVersion packageVersion,
        NuGetv3LocalRepository localRepository,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageSourceLocation packageSourceLocation,
        bool givenSpecificVersion,
        DirectoryPath assetFileDirectory,
        string targetFramework,
        DirectoryPath packageRootDirectory,
        string runtimeJsonPath,
        IToolPackageStore toolPackageStore,
        DirectoryPath globalToolStageDir)
    {
        string rollbackDirectory = null;
        rollbackDirectory = isGlobalTool ? toolDownloadDir.Value : new VersionFolderPathResolver(toolDownloadDir.Value).GetInstallPath(packageId.ToString(), packageVersion);
        
        return TransactionalAction.Run(() =>
        {
            DirectoryPath toolReturnPackageDirectory;
            DirectoryPath toolReturnJsonParentDirectory;

            var package = localRepository.FindPackage(packageId.ToString(), packageVersion);

            if (package == null)
            {
                DownloadAndExtractPackage(packageId, nugetPackageDownloader, toolDownloadDir.Value, packageVersion, packageSourceLocation, includeUnlisted: givenSpecificVersion).GetAwaiter().GetResult();
            }
            else if (isGlobalTool)
            {
                throw new ToolPackageException(
                    string.Format(
                        CliStrings.ToolPackageConflictPackageId,
                        packageId,
                        packageVersion.ToNormalizedString()));
            }

            CreateAssetFile(packageId, packageVersion, toolDownloadDir, assetFileDirectory, runtimeJsonPath, targetFramework);

            if (isGlobalTool)
            {
                toolReturnPackageDirectory = toolPackageStore.GetPackageDirectory(packageId, packageVersion);
                toolReturnJsonParentDirectory = toolPackageStore.GetPackageDirectory(packageId, packageVersion);
                var rootPackageDirectory = toolPackageStore.GetRootPackageDirectory(packageId);
                Directory.CreateDirectory(rootPackageDirectory.Value);
                FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(globalToolStageDir.Value, toolReturnPackageDirectory.Value));
                rollbackDirectory = toolReturnPackageDirectory.Value;
            }
            else
            {
                toolReturnPackageDirectory = toolDownloadDir;
                toolReturnJsonParentDirectory = assetFileDirectory;
            }

            return (toolReturnPackageDirectory, toolReturnJsonParentDirectory);
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
        ToolPackageInstance toolPackageInstance
        )
    {
        var runtimeConfigFilePath = Path.ChangeExtension(toolPackageInstance.Command.Executable.Value, ".runtimeconfig.json");

        // Update the runtimeconfig.json file
        if (File.Exists(runtimeConfigFilePath))
        {
            string existingJson = File.ReadAllText(runtimeConfigFilePath);

            var jsonObject = JObject.Parse(existingJson);
            if (jsonObject["runtimeOptions"] is JObject runtimeOptions)
            {
                runtimeOptions["rollForward"] = "Major";
                string updateJson = jsonObject.ToString();
                File.WriteAllText(runtimeConfigFilePath, updateJson);
            }
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
                    if (item.Properties.TryGetValue("locale", out var locale))
                    {
                        newItem.Properties["locale"] = (string)locale;
                    }

                    if (item.Properties.TryGetValue("related", out var related))
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
        PackageId packageId,
        INuGetPackageDownloader nugetPackageDownloader,
        string packagesRootPath,
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
            PackageArchiveReader reader = new(packageStream);
            version = new NuspecReader(reader.GetNuspec()).GetVersion();

            var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
            var hashPath = new VersionFolderPathResolver(packagesRootPath).GetHashPath(packageId.ToString(), version);

            Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
            File.WriteAllText(hashPath, packageHash);
        }

        // Extract the package
        var nupkgDir = new VersionFolderPathResolver(packagesRootPath).GetInstallPath(packageId.ToString(), version);
        await nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(nupkgDir));

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
            //  TODO: What about DotnetToolRidPackage type?
            PackageType = [PackageType.DotnetTool]
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
        //  TODO: What about DotnetToolRidPackage type?
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

    public NuGetVersion GetNuGetVersion(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange versionRange = null,
        bool isGlobalTool = false,
        RestoreActionConfig restoreActionConfig = null)
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

        var nugetPackageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(
            packageInstallDir: isGlobalTool ? _globalToolStageDir : _localToolDownloadDir,
            verboseLogger: nugetLogger,
            shouldUsePackageSourceMapping: true,
            verbosityOptions: verbosity,
            restoreActionConfig: restoreActionConfig);

        var packageSourceLocation = new PackageSourceLocation(
            nugetConfig: packageLocation.NugetConfig,
            rootConfigDirectory: packageLocation.RootConfigDirectory,
            sourceFeedOverrides: packageLocation.SourceFeedOverrides,
            additionalSourceFeeds: packageLocation.AdditionalFeeds);

        return nugetPackageDownloader.GetBestPackageVersionAsync(packageId, versionRange, packageSourceLocation).GetAwaiter().GetResult();
    }
}
