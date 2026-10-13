// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal abstract class ToolPackageDownloaderBase : IToolPackageDownloader
{
    private static readonly TimeSpan s_lockInitialWaitTimeout = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan s_lockRetryDelay = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan s_lockWaitTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions s_writeIndentedOptions = new() { WriteIndented = true };
    private const int WindowsSharingViolationHResult = unchecked((int)0x80070020);
    private const int WindowsLockViolationHResult = unchecked((int)0x80070021);
    private const int LinuxWouldBlockError = 11;
    private const int MacOSWouldBlockError = 35;

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
        _runtimeJsonPath = runtimeJsonPathForTests ?? Path.Combine(AppContext.BaseDirectory!, "PortableRuntimeIdentifierGraph.json");
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
        VerbosityOptions verbosity,
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
        VerbosityOptions verbosity,
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

        var packageSourceLocation = new PackageSourceLocation(packageLocation.NugetConfig, packageLocation.RootConfigDirectory, packageLocation.SourceFeedOverrides, packageLocation.AdditionalFeeds, _currentWorkingDirectory, packageLocation.PackageSourceOverrides);

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
                isGlobalToolRollForward,
                verbosity: verbosity);
        }
        else
        {
            return InstallLocalToolPackageInternal(
                packageSourceLocation,
                nugetPackageDownloader,
                packageId,
                packageVersion,
                givenSpecificVersion,
                targetFramework,
                verbosity: verbosity);
        }
    }

    protected IToolPackage InstallGlobalToolPackageInternal(
        PackageSourceLocation packageSourceLocation,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageId packageId,
        NuGetVersion packageVersion,
        bool givenSpecificVersion,
        string? targetFramework,
        bool isGlobalToolRollForward,
        VerbosityOptions verbosity)
    {
        using FileStream installLock = AcquireToolInstallLock(_toolPackageStore.Root, packageId, packageVersion);

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
                    targetFramework,
                    verbosity);

                var toolStoreTargetDirectory = _toolPackageStore.GetPackageDirectory(packageId, packageVersion);

                //  Create parent directory in global tool store, for example dotnet\tools\.store\powershell
                _fileSystem.Directory.CreateDirectory(toolStoreTargetDirectory.GetParentPath().Value);

                var _moveContentActivity = Activities.Source.StartActivity("move-global-tool-content");
                //  Move tool files from stage to final location
                FileAccessRetrier.RetryOnMoveAccessFailure(() => _fileSystem.Directory.Move(_globalToolStageDir.Value, toolStoreTargetDirectory.Value));
                _moveContentActivity?.Dispose();

                rollbackDirectory = toolStoreTargetDirectory.Value;

                var toolPackageInstance = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: toolStoreTargetDirectory,
                    assetsJsonParentDirectory: toolStoreTargetDirectory,
                    fileSystem: _fileSystem);

                if (isGlobalToolRollForward)
                {
                    if (verbosity.IsDetailedOrDiagnostic())
                    {
                        Reporter.Output.WriteLine($"Configuring package {packageId}@{packageVersion} for runtime roll-forward");
                    }
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

    /// <summary>
    /// Serializes installation of one package version so concurrent processes cannot mutate the same files.
    /// </summary>
    /// <remarks>
    /// The lock file persists, but only its open <see cref="FileShare.None"/> handle represents ownership. A terminated
    /// process therefore releases the lock without requiring stale-file cleanup. Prolonged contention is reported and
    /// eventually fails with a timeout.
    /// </remarks>
    internal static FileStream AcquireToolInstallLock(
        DirectoryPath packageInstallRoot,
        PackageId packageId,
        NuGetVersion packageVersion,
        TimeSpan? initialWaitTimeout = null,
        TimeSpan? waitTimeout = null,
        IReporter? errorReporter = null)
    {
        string lockFilePath = GetToolInstallLockFilePath(packageInstallRoot, packageId, packageVersion);
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);

        initialWaitTimeout ??= s_lockInitialWaitTimeout;
        waitTimeout ??= s_lockWaitTimeout;
        errorReporter ??= Reporter.Error;
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool waitingReported = false;

        while (true)
        {
            try
            {
                FileStream lockStream = new(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                return lockStream;
            }
            catch (IOException exception) when (IsToolInstallLockContention(exception))
            {
                if (!waitingReported && stopwatch.Elapsed >= initialWaitTimeout)
                {
                    FileLockOwner? lockOwner = FileLockOwnerFinder.TryGetFileLockOwner(lockFilePath);
                    string message = lockOwner is not null
                        ? string.Format(
                            CliStrings.ToolInstallationWaitingWithProcess,
                            packageId,
                            packageVersion.ToNormalizedString(),
                            lockOwner.ProcessId,
                            lockOwner.ProcessName)
                        : string.Format(
                            CliStrings.ToolInstallationWaiting,
                            packageId,
                            packageVersion.ToNormalizedString());
                    errorReporter.WriteLine(message);
                    waitingReported = true;
                }

                if (stopwatch.Elapsed >= waitTimeout)
                {
                    throw new ToolPackageException(string.Format(
                        CliStrings.ToolInstallationTimeout,
                        packageId,
                        packageVersion.ToNormalizedString()));
                }

                Thread.Sleep(s_lockRetryDelay);
            }
        }
    }

    /// <summary>
    /// Creates a stable per-root lock path so only installations of the same package version contend.
    /// </summary>
    /// <remarks>
    /// The normalized, case-insensitive package identity is hashed to produce a fixed-length valid file name under
    /// the package root's lock directory.
    /// </remarks>
    internal static string GetToolInstallLockFilePath(
        DirectoryPath packageInstallRoot,
        PackageId packageId,
        NuGetVersion packageVersion)
    {
        string lockIdentity = $"{packageId.ToString().ToLowerInvariant()}\n{packageVersion.ToNormalizedString().ToLowerInvariant()}";
        string lockFileName = $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockIdentity)))}.lock";
        return Path.Combine(
            Path.GetFullPath(packageInstallRoot.Value),
            ToolPackageStoreAndQuery.LockDirectory,
            lockFileName);
    }

    /// <summary>
    /// Distinguishes retryable lock contention from I/O failures that must be surfaced immediately.
    /// </summary>
    /// <remarks>
    /// <see cref="FileStream"/> exposes the relevant Windows sharing errors and Unix <c>EWOULDBLOCK</c> values through
    /// <see cref="Exception.HResult"/>.
    /// </remarks>
    internal static bool IsToolInstallLockContention(IOException exception) =>
        OperatingSystem.IsWindows()
            ? exception.HResult is WindowsSharingViolationHResult or WindowsLockViolationHResult
            : exception.HResult is LinuxWouldBlockError or MacOSWouldBlockError;

    protected IToolPackage InstallLocalToolPackageInternal(
        PackageSourceLocation packageSourceLocation,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageId packageId,
        NuGetVersion packageVersion,
        bool givenSpecificVersion,
        string? targetFramework,
        VerbosityOptions verbosity)
    {
        using FileStream installLock = AcquireToolInstallLock(_localToolDownloadDir, packageId, packageVersion);

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
                    targetFramework,
                    verbosity);

                var toolPackageInstance = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: _localToolDownloadDir,
                    assetsJsonParentDirectory: _localToolAssetDir,
                    fileSystem: _fileSystem);

                return toolPackageInstance;
            });
    }

    protected void DownloadTool(
        DirectoryPath packageDownloadDir,
        PackageId packageId,
        NuGetVersion packageVersion,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageSourceLocation packageSourceLocation,
        bool givenSpecificVersion,
        DirectoryPath assetFileDirectory,
        string? targetFramework,
        VerbosityOptions verbosity)
    {

        if (!IsPackageInstalled(packageId, packageVersion, packageDownloadDir.Value))
        {
            DownloadAndExtractPackage(packageId, nugetPackageDownloader, packageDownloadDir.Value, packageVersion, packageSourceLocation, includeUnlisted: givenSpecificVersion, verbosity: verbosity);
        }

        CreateAssetFile(packageId, packageVersion, packageDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.AssetsFileName), _runtimeJsonPath, verbosity, targetFramework);

        //  Also download RID-specific package if needed
        if (ResolveRidSpecificPackage(packageId, packageVersion, packageDownloadDir, assetFileDirectory, verbosity) is PackageId ridSpecificPackage)
        {
            if (!IsPackageInstalled(ridSpecificPackage, packageVersion, packageDownloadDir.Value))
            {
                DownloadAndExtractPackage(ridSpecificPackage, nugetPackageDownloader, packageDownloadDir.Value, packageVersion, packageSourceLocation, includeUnlisted: true, verbosity: verbosity);
            }

            CreateAssetFile(ridSpecificPackage, packageVersion, packageDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.RidSpecificPackageAssetsFileName), _runtimeJsonPath, verbosity, targetFramework);
        }
    }

    public bool TryGetDownloadedTool(
        PackageId packageId,
        NuGetVersion packageVersion,
        string? targetFramework,
        VerbosityOptions verbosity,
        [NotNullWhen(true)]
        out IToolPackage? toolPackage)
    {
        if (!IsPackageInstalled(packageId, packageVersion, _localToolDownloadDir.Value))
        {
            toolPackage = null;
            return false;
        }
        CreateAssetFile(packageId, packageVersion, _localToolDownloadDir, Path.Combine(_localToolAssetDir.Value, ToolPackageInstance.AssetsFileName), _runtimeJsonPath, verbosity, targetFramework);

        if (ResolveRidSpecificPackage(packageId, packageVersion, _localToolDownloadDir, _localToolAssetDir, verbosity) is PackageId ridSpecificPackage)
        {
            if (!IsPackageInstalled(ridSpecificPackage, packageVersion, _localToolDownloadDir.Value))
            {
                toolPackage = null;
                return false;
            }
            CreateAssetFile(ridSpecificPackage, packageVersion, _localToolDownloadDir,
                Path.Combine(_localToolAssetDir.Value, ToolPackageInstance.RidSpecificPackageAssetsFileName), _runtimeJsonPath, verbosity, targetFramework);
        }

        toolPackage = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: _localToolDownloadDir,
                    assetsJsonParentDirectory: _localToolAssetDir,
                    fileSystem: _fileSystem);
        return true;

    }

    public bool TryGetBestDownloadedTool(
        PackageId packageId,
        VersionRange versionRange,
        string? targetFramework,
        VerbosityOptions verbosity,
        [NotNullWhen(true)] out IToolPackage? toolPackage)
    {
        string packageRootDirectory = Path.Combine(_localToolDownloadDir.Value, packageId.ToString());
        if (!_fileSystem.Directory.Exists(packageRootDirectory))
        {
            toolPackage = null;
            return false;
        }

        IEnumerable<NuGetVersion> installedVersions = _fileSystem.Directory
            .EnumerateDirectories(packageRootDirectory)
            .Select(Path.GetFileName)
            .Select(version => NuGetVersion.TryParse(version, out NuGetVersion? parsedVersion) ? parsedVersion : null)
            .Where(version => version is not null && IsPackageInstalled(packageId, version, _localToolDownloadDir.Value))
            .Cast<NuGetVersion>();

        NuGetVersion? bestVersion = versionRange.FindBestMatch(installedVersions);
        if (bestVersion is null)
        {
            toolPackage = null;
            return false;
        }

        return TryGetDownloadedTool(packageId, bestVersion, targetFramework, verbosity, out toolPackage);
    }

    private PackageId? ResolveRidSpecificPackage(PackageId packageId,
        NuGetVersion packageVersion,
        DirectoryPath packageDownloadDir,
        DirectoryPath assetFileDirectory,
        VerbosityOptions verbosity)
    {
        var toolConfiguration = GetToolConfiguration(packageId, packageDownloadDir, assetFileDirectory);

        if (toolConfiguration.RidSpecificPackages?.Any() == true)
        {
            if (verbosity.IsDetailedOrDiagnostic())
            {
                Reporter.Output.WriteLine($"Resolving RID-specific package for {packageId} {packageVersion}");
                Reporter.Output.WriteLine($"Target RID: {RuntimeInformation.RuntimeIdentifier}");
                Reporter.Output.WriteLine($"Available RID-specific packages: {string.Join(", ", toolConfiguration.RidSpecificPackages.Keys)}");
            }
            var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(_runtimeJsonPath);
            var bestRuntimeIdentifier = Microsoft.NET.Build.Tasks.NuGetUtils.GetBestMatchingRid(runtimeGraph, RuntimeInformation.RuntimeIdentifier, toolConfiguration.RidSpecificPackages.Keys, out bool wasInGraph);
            if (bestRuntimeIdentifier == null)
            {
                throw new ToolPackageException(string.Format(CliStrings.ToolUnsupportedRuntimeIdentifier, RuntimeInformation.RuntimeIdentifier,
                    string.Join(" ", toolConfiguration.RidSpecificPackages.Keys)));
            }

            var resolvedPackage = toolConfiguration.RidSpecificPackages[bestRuntimeIdentifier];
            if (verbosity.IsDetailedOrDiagnostic())
            {
                Reporter.Output.WriteLine($"Best matching RID: {bestRuntimeIdentifier}");
                Reporter.Output.WriteLine($"Resolved package: {resolvedPackage}");
            }
            if (resolvedPackage is PackageIdentity p)
            {
                return new PackageId(p.Id);
            }
            return null;
        }

        if (verbosity.IsDetailedOrDiagnostic())
        {
            Reporter.Output.WriteLine($"No RID-specific package declared for {packageId} {packageVersion}.");
        }

        return null;
    }

    protected void UpdateRuntimeConfig(
        ToolPackageInstance toolPackageInstance
        )
    {
        using var _updateRuntimeConfigActivity = Activities.Source.StartActivity("update-runtimeconfig");
        var runtimeConfigFilePath = Path.ChangeExtension(toolPackageInstance.Command.Executable.Value, ".runtimeconfig.json");

        // Update the runtimeconfig.json file
        if (_fileSystem.File.Exists(runtimeConfigFilePath))
        {
            string existingJson = _fileSystem.File.ReadAllText(runtimeConfigFilePath);

            var jsonObject = JsonNode.Parse(existingJson)!.AsObject();
            if (jsonObject["runtimeOptions"] is JsonObject runtimeOptions)
            {
                runtimeOptions["rollForward"] = "Major";
                string updateJson = jsonObject.ToJsonString(s_writeIndentedOptions);
                _fileSystem.File.WriteAllText(runtimeConfigFilePath, updateJson);
            }
        }
    }

    public async Task<(NuGetVersion version, PackageSource source)> GetNuGetVersionAsync(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default)
    {
        versionRange ??= VersionRange.Parse("*");

        var nugetPackageDownloader = CreateNuGetPackageDownloader(
            verifySignatures: false,
            verbosity,
            restoreActionConfig);

        var packageSourceLocation = new PackageSourceLocation(
            nugetConfig: packageLocation.NugetConfig,
            rootConfigDirectory: packageLocation.RootConfigDirectory,
            sourceFeedOverrides: packageLocation.SourceFeedOverrides,
            additionalSourceFeeds: packageLocation.AdditionalFeeds,
            basePath: _currentWorkingDirectory);

        return await nugetPackageDownloader
            .GetBestPackageVersionAndSourceAsync(
                packageId,
                versionRange,
                packageSourceLocation,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
