// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Commands;
using NuGet.Commands.Restore;
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

internal abstract class ToolPackageDownloaderBase : IToolPackageDownloader
{
    private readonly IToolPackageStore _toolPackageStore;

    protected readonly IFileSystem _fileSystem;

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

    protected readonly string? _currentWorkingDirectory;

    protected ToolPackageDownloaderBase(
        IToolPackageStore store,
        string? runtimeJsonPathForTests = null,
        string? currentWorkingDirectory = null,
        IFileSystem? fileSystem = null
    )
    {
        _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _globalToolStageDir = _toolPackageStore.GetRandomStagingDirectory();
        //  NuGet settings can't use mock file system.  This means in testing we will get the real global packages folder, but that is fine because we
        //  mock the whole file system anyway.
        ISettings settings = Settings.LoadDefaultSettings(currentWorkingDirectory ?? Directory.GetCurrentDirectory());
        _localToolDownloadDir = new DirectoryPath(SettingsUtility.GetGlobalPackagesFolder(settings));
        _currentWorkingDirectory = currentWorkingDirectory;

        _localToolAssetDir = new DirectoryPath(_fileSystem.Directory.CreateTemporarySubdirectory());
        _runtimeJsonPath = runtimeJsonPathForTests ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "RuntimeIdentifierGraph.json");
    }

    protected abstract INuGetPackageDownloader CreateNuGetPackageDownloader(
        bool verifySignatures,
        VerbosityOptions verbosity,
        RestoreActionConfig? restoreActionConfig);

    protected abstract NuGetVersion DownloadAndExtractPackage(
        PackageId packageId,
        INuGetPackageDownloader nugetPackageDownloader,
        string packagesRootPath,
        NuGetVersion packageVersion,
        PackageSourceLocation packageSourceLocation,
        bool includeUnlisted = false
    );

    protected abstract bool IsPackageInstalled(
        PackageId packageId,
        NuGetVersion packageVersion,
        string packagesRootPath);

    protected abstract void CreateAssetFile(
        PackageId packageId,
        NuGetVersion version,
        DirectoryPath packagesRootPath,
        string assetFilePath,
        string runtimeJsonGraph,
        string? targetFramework = null);

    protected abstract ToolConfiguration GetToolConfiguration(PackageId id,
        DirectoryPath packageDirectory,
        DirectoryPath assetsJsonParentDirectory);

    public IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
        VerbosityOptions verbosity = VerbosityOptions.normal,
        VersionRange? versionRange = null,
        string? targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig? restoreActionConfig = null)
    {
        if (versionRange == null)
        {
            var versionString = "*";
            versionRange = VersionRange.Parse(versionString);
        }

        var nugetPackageDownloader = CreateNuGetPackageDownloader(
            verifySignatures,
            verbosity,
            restoreActionConfig);

        var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, packageLocation.SourceFeedOverrides, packageLocation.AdditionalFeeds, _currentWorkingDirectory);

        NuGetVersion packageVersion = nugetPackageDownloader.GetBestPackageVersionAsync(packageId, versionRange, packageSourceLocation).GetAwaiter().GetResult();

        bool givenSpecificVersion = false;
        if (versionRange.MinVersion != null && versionRange.MaxVersion != null && versionRange.MinVersion == versionRange.MaxVersion)
        {
            givenSpecificVersion = true;
        }

        if (isGlobalTool)
        {
            return InstallGlobalToolPackageInternal(
                packageSourceLocation,
                nugetPackageDownloader,
                packageId,
                packageVersion,
                givenSpecificVersion,
                targetFramework,
                isGlobalToolRollForward);
        }
        else
        {
            return InstallLocalToolPackageInternal(
                packageSourceLocation,
                nugetPackageDownloader,
                packageId,
                packageVersion,
                givenSpecificVersion,
                targetFramework);
        }
    }

    protected IToolPackage InstallGlobalToolPackageInternal(
        PackageSourceLocation packageSourceLocation,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageId packageId,
        NuGetVersion packageVersion,
        bool givenSpecificVersion,
        string? targetFramework,
        bool isGlobalToolRollForward)
    {
        // Check if package already exists in global tools location
        var nugetPackageRootDirectory = new VersionFolderPathResolver(_toolPackageStore.Root.Value).GetInstallPath(packageId.ToString(), packageVersion);
        if (IsPackageInstalled(packageId, packageVersion, nugetPackageRootDirectory))
        {
            throw new ToolPackageException(
                string.Format(
                    CliStrings.ToolPackageConflictPackageId,
                    packageId,
                    packageVersion.ToNormalizedString()));
        }

        string rollbackDirectory = _globalToolStageDir.Value;

        return TransactionalAction.Run<IToolPackage>(
            action: () =>
            {
                DownloadTool(
                    packageDownloadDir: _globalToolStageDir,
                    packageId,
                    packageVersion,
                    nugetPackageDownloader,
                    packageSourceLocation,
                    givenSpecificVersion,
                    assetFileDirectory: _globalToolStageDir,
                    targetFramework);

                var toolStoreTargetDirectory = _toolPackageStore.GetPackageDirectory(packageId, packageVersion);

                //  Create parent directory in global tool store, for example dotnet\tools\.store\powershell
                _fileSystem.Directory.CreateDirectory(toolStoreTargetDirectory.GetParentPath().Value);

                //  Move tool files from stage to final location
                FileAccessRetrier.RetryOnMoveAccessFailure(() => _fileSystem.Directory.Move(_globalToolStageDir.Value, toolStoreTargetDirectory.Value));

                rollbackDirectory = toolStoreTargetDirectory.Value;

                var toolPackageInstance = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: toolStoreTargetDirectory,
                    assetsJsonParentDirectory: toolStoreTargetDirectory,
                    fileSystem: _fileSystem);

                if (isGlobalToolRollForward)
                {
                    UpdateRuntimeConfig(toolPackageInstance);
                }

                return toolPackageInstance;
            },
            rollback: () =>
            {
                if (rollbackDirectory != null && _fileSystem.Directory.Exists(rollbackDirectory))
                {
                    _fileSystem.Directory.Delete(rollbackDirectory, true);
                }

                //  Delete global tool store package ID directory if it's empty (ie no other versions are installed)
                DirectoryPath packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);
                if (_fileSystem.Directory.Exists(packageRootDirectory.Value) &&
                    !_fileSystem.Directory.EnumerateFileSystemEntries(packageRootDirectory.Value).Any())
                {
                    _fileSystem.Directory.Delete(packageRootDirectory.Value, false);
                }
            });
    }

    protected IToolPackage InstallLocalToolPackageInternal(
        PackageSourceLocation packageSourceLocation,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageId packageId,
        NuGetVersion packageVersion,
        bool givenSpecificVersion,
        string? targetFramework)
    {
        return TransactionalAction.Run<IToolPackage>(
            action: () =>
            {
                DownloadTool(
                    packageDownloadDir: _localToolDownloadDir,
                    packageId,
                    packageVersion,
                    nugetPackageDownloader,
                    packageSourceLocation,
                    givenSpecificVersion,
                    assetFileDirectory: _localToolAssetDir,
                    targetFramework);

                var toolPackageInstance = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: _localToolDownloadDir,
                    assetsJsonParentDirectory: _localToolAssetDir,
                    fileSystem: _fileSystem);

                return toolPackageInstance;
            });
    }

    protected virtual void DownloadTool(
        DirectoryPath packageDownloadDir,
        PackageId packageId,
        NuGetVersion packageVersion,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageSourceLocation packageSourceLocation,
        bool givenSpecificVersion,
        DirectoryPath assetFileDirectory,
        string? targetFramework)
    {

        if (!IsPackageInstalled(packageId, packageVersion, packageDownloadDir.Value))
        {
            DownloadAndExtractPackage(packageId, nugetPackageDownloader, packageDownloadDir.Value, packageVersion, packageSourceLocation, includeUnlisted: givenSpecificVersion);
        }

        CreateAssetFile(packageId, packageVersion, packageDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.AssetsFileName), _runtimeJsonPath, targetFramework);

        //  Also download RID-specific package if needed
        if (ResolveRidSpecificPackage(packageId, packageVersion, packageDownloadDir, assetFileDirectory) is PackageId ridSpecificPackage)
        {
            if (!IsPackageInstalled(ridSpecificPackage, packageVersion, packageDownloadDir.Value))
            {
                DownloadAndExtractPackage(ridSpecificPackage, nugetPackageDownloader, packageDownloadDir.Value, packageVersion, packageSourceLocation, includeUnlisted: true);
            }

            CreateAssetFile(ridSpecificPackage, packageVersion, packageDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.RidSpecificPackageAssetsFileName), _runtimeJsonPath, targetFramework);
        }
    }

    public bool TryGetDownloadedTool(
        PackageId packageId,
        NuGetVersion packageVersion,
        string targetFramework,
        out IToolPackage? toolPackage)
    {
        if (!IsPackageInstalled(packageId, packageVersion, _localToolDownloadDir.Value))
        {
            toolPackage = null;
            return false;
        }
        CreateAssetFile(packageId, packageVersion, _localToolDownloadDir, Path.Combine(_localToolAssetDir.Value, ToolPackageInstance.AssetsFileName), _runtimeJsonPath, targetFramework);

        if (ResolveRidSpecificPackage(packageId, packageVersion, _localToolDownloadDir, _localToolAssetDir) is PackageId ridSpecificPackage)
        {
            if (!IsPackageInstalled(ridSpecificPackage, packageVersion, _localToolDownloadDir.Value))
            {
                toolPackage = null;
                return false;
            }
            CreateAssetFile(ridSpecificPackage, packageVersion, _localToolDownloadDir,
                Path.Combine(_localToolAssetDir.Value, ToolPackageInstance.RidSpecificPackageAssetsFileName), _runtimeJsonPath, targetFramework);
        }

        toolPackage = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: _localToolDownloadDir,
                    assetsJsonParentDirectory: _localToolAssetDir,
                    fileSystem: _fileSystem);
        return true;

    }

    private PackageId? ResolveRidSpecificPackage(PackageId packageId,
        NuGetVersion packageVersion,
        DirectoryPath packageDownloadDir,
        DirectoryPath assetFileDirectory)
    {
        var toolConfiguration = GetToolConfiguration(packageId, packageDownloadDir, assetFileDirectory);

        if (toolConfiguration.RidSpecificPackages?.Any() == true)
        {
            var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(_runtimeJsonPath);
            var bestRuntimeIdentifier = Microsoft.NET.Build.Tasks.NuGetUtils.GetBestMatchingRid(runtimeGraph, RuntimeInformation.RuntimeIdentifier, toolConfiguration.RidSpecificPackages.Keys, out bool wasInGraph);
            if (bestRuntimeIdentifier == null)
            {
                throw new ToolPackageException(string.Format(CliStrings.ToolUnsupportedRuntimeIdentifier, RuntimeInformation.RuntimeIdentifier,
                    string.Join(" ", toolConfiguration.RidSpecificPackages.Keys)));
            }

            var resolvedPackage = toolConfiguration.RidSpecificPackages[bestRuntimeIdentifier];

            if (resolvedPackage is PackageIdentity p)
            {
                return new PackageId(p.Id);
            }
            return null;
        }

        return null;
    }

    protected void UpdateRuntimeConfig(
        ToolPackageInstance toolPackageInstance
        )
    {
        var runtimeConfigFilePath = Path.ChangeExtension(toolPackageInstance.Command.Executable.Value, ".runtimeconfig.json");

        // Update the runtimeconfig.json file
        if (_fileSystem.File.Exists(runtimeConfigFilePath))
        {
            string existingJson = _fileSystem.File.ReadAllText(runtimeConfigFilePath);

            var jsonObject = JObject.Parse(existingJson);
            if (jsonObject["runtimeOptions"] is JObject runtimeOptions)
            {
                runtimeOptions["rollForward"] = "Major";
                string updateJson = jsonObject.ToString();
                _fileSystem.File.WriteAllText(runtimeConfigFilePath, updateJson);
            }
        }
    }

    public virtual (NuGetVersion version, PackageSource source) GetNuGetVersion(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        RestoreActionConfig? restoreActionConfig = null)
    {
        if (versionRange == null)
        {
            var versionString = "*";
            versionRange = VersionRange.Parse(versionString);
        }

        var nugetPackageDownloader = CreateNuGetPackageDownloader(
            false,
            verbosity,
            restoreActionConfig);

        var packageSourceLocation = new PackageSourceLocation(
            nugetConfig: packageLocation.NugetConfig,
            rootConfigDirectory: packageLocation.RootConfigDirectory,
            sourceFeedOverrides: packageLocation.SourceFeedOverrides,
            additionalSourceFeeds: packageLocation.AdditionalFeeds,
            basePath: _currentWorkingDirectory);

        return nugetPackageDownloader.GetBestPackageVersionAndSourceAsync(packageId, versionRange, packageSourceLocation).GetAwaiter().GetResult();
    }
}
