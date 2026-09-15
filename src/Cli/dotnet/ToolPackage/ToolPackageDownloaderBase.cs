// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Transactions;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal abstract class ToolPackageDownloaderBase : IToolPackageDownloader
{
    private sealed class ToolPackageLockEnlistment(FileStream lockFile, string transactionLockKey) : IEnlistmentNotification
    {
        private readonly List<Action> _rollbackActions = [];
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            Func<Action?> rollbackOnTransaction,
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                T result = await action().ConfigureAwait(false);
                if (rollbackOnTransaction() is Action rollback)
                {
                    _rollbackActions.Add(rollback);
                }

                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Commit(Enlistment enlistment)
        {
            s_transactionLocks.TryRemove(transactionLockKey, out _);
            lockFile.Dispose();
            enlistment.Done();
        }

        public void InDoubt(Enlistment enlistment) => Rollback(enlistment);

        public void Prepare(PreparingEnlistment enlistment) => enlistment.Prepared();

        public void Rollback(Enlistment enlistment)
        {
            try
            {
                s_transactionLocks.TryRemove(transactionLockKey, out _);
                for (int i = _rollbackActions.Count - 1; i >= 0; i--)
                {
                    _rollbackActions[i]();
                }
            }
            finally
            {
                lockFile.Dispose();
                enlistment.Done();
            }
        }
    }

    private static readonly JsonSerializerOptions s_writeIndentedOptions = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, ToolPackageLockEnlistment> s_transactionLocks =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private static readonly TimeSpan s_lockRetryDelay = TimeSpan.FromMilliseconds(10);
    private const int WindowsSharingViolationHResult = unchecked((int)0x80070020);
    private const int WindowsLockViolationHResult = unchecked((int)0x80070021);
    private const int LinuxWouldBlockError = 11;
    private const int MacOSWouldBlockError = 35;
    private readonly TimeSpan _lockInitialWaitTimeout;
    private readonly TimeSpan _lockWaitTimeout;

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
        IFileSystem? fileSystem = null,
        TimeSpan? lockInitialWaitTimeout = null,
        TimeSpan? lockWaitTimeout = null
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
        _lockInitialWaitTimeout = lockInitialWaitTimeout ?? TimeSpan.FromMilliseconds(50);
        _lockWaitTimeout = lockWaitTimeout ?? TimeSpan.FromMinutes(5);
    }

    protected abstract INuGetPackageDownloader CreateNuGetPackageDownloader(
        bool verifySignatures,
        VerbosityOptions verbosity,
        RestoreActionConfig? restoreActionConfig);

    protected abstract Task<NuGetVersion> DownloadAndExtractPackageAsync(
        PackageId packageId,
        INuGetPackageDownloader nugetPackageDownloader,
        string packagesRootPath,
        NuGetVersion packageVersion,
        PackageSourceLocation packageSourceLocation,
        VerbosityOptions verbosity,
        bool includeUnlisted,
        CancellationToken cancellationToken
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

    public async Task<IToolPackage> InstallPackageAsync(PackageLocation packageLocation, PackageId packageId,
        VerbosityOptions verbosity = VerbosityOptions.normal,
        VersionRange? versionRange = null,
        string? targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default)
    {
        Transaction? transaction = Transaction.Current;

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

        NuGetVersion packageVersion = await nugetPackageDownloader
            .GetBestPackageVersionAsync(packageId, versionRange, packageSourceLocation, cancellationToken)
            .ConfigureAwait(false);

        bool givenSpecificVersion = false;
        if (versionRange.MinVersion != null && versionRange.MaxVersion != null && versionRange.MinVersion == versionRange.MaxVersion)
        {
            givenSpecificVersion = true;
        }

        if (isGlobalTool)
        {
            return await InstallGlobalToolPackageInternalAsync(
                packageSourceLocation,
                nugetPackageDownloader,
                packageId,
                packageVersion,
                givenSpecificVersion,
                targetFramework,
                isGlobalToolRollForward,
                verbosity,
                cancellationToken,
                transaction).ConfigureAwait(false);
        }
        else
        {
            return await InstallLocalToolPackageInternalAsync(
                packageSourceLocation,
                nugetPackageDownloader,
                packageId,
                packageVersion,
                givenSpecificVersion,
                targetFramework,
                verbosity,
                cancellationToken,
                transaction).ConfigureAwait(false);
        }
    }

    protected Task<IToolPackage> InstallGlobalToolPackageInternalAsync(
        PackageSourceLocation packageSourceLocation,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageId packageId,
        NuGetVersion packageVersion,
        bool givenSpecificVersion,
        string? targetFramework,
        bool isGlobalToolRollForward,
        VerbosityOptions verbosity,
        CancellationToken cancellationToken,
        Transaction? transaction)
    {
        string rollbackDirectory = _globalToolStageDir.Value;

        return ExecuteWithToolInstallLockAsync(
            _toolPackageStore.Root,
            packageId,
            packageVersion,
            cancellationToken,
            () => TransactionalAction.RunAsync<IToolPackage>(
                action: async () =>
                {
                    var nugetPackageRootDirectory = new VersionFolderPathResolver(_toolPackageStore.Root.Value).GetInstallPath(packageId.ToString(), packageVersion);
                    if (IsPackageInstalled(packageId, packageVersion, nugetPackageRootDirectory))
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CliStrings.ToolPackageConflictPackageId,
                                packageId,
                                packageVersion.ToNormalizedString()));
                    }

                    await DownloadToolAsync(
                        packageDownloadDir: _globalToolStageDir,
                        packageId,
                        packageVersion,
                        nugetPackageDownloader,
                        packageSourceLocation,
                        givenSpecificVersion,
                        assetFileDirectory: _globalToolStageDir,
                        targetFramework,
                        verbosity,
                        lockPackageDownloads: false,
                        cancellationToken).ConfigureAwait(false);

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
                },
                transaction: transaction));
    }

    protected Task<IToolPackage> InstallLocalToolPackageInternalAsync(
        PackageSourceLocation packageSourceLocation,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageId packageId,
        NuGetVersion packageVersion,
        bool givenSpecificVersion,
        string? targetFramework,
        VerbosityOptions verbosity,
        CancellationToken cancellationToken,
        Transaction? transaction)
    {
        DirectoryPath assetFileDirectory = GetLocalToolAssetDirectory(packageId, packageVersion);

        return TransactionalAction.RunAsync<IToolPackage>(
            action: async () =>
            {
                _fileSystem.Directory.CreateDirectory(assetFileDirectory.Value);

                await DownloadToolAsync(
                    packageDownloadDir: _localToolDownloadDir,
                    packageId,
                    packageVersion,
                    nugetPackageDownloader,
                    packageSourceLocation,
                    givenSpecificVersion,
                    assetFileDirectory,
                    targetFramework,
                    verbosity,
                    lockPackageDownloads: true,
                    cancellationToken).ConfigureAwait(false);

                var toolPackageInstance = new ToolPackageInstance(id: packageId,
                    version: packageVersion,
                    packageDirectory: _localToolDownloadDir,
                    assetsJsonParentDirectory: assetFileDirectory,
                    fileSystem: _fileSystem);

                return toolPackageInstance;
            },
            transaction: transaction);
    }

    protected async Task DownloadToolAsync(
        DirectoryPath packageDownloadDir,
        PackageId packageId,
        NuGetVersion packageVersion,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageSourceLocation packageSourceLocation,
        bool givenSpecificVersion,
        DirectoryPath assetFileDirectory,
        string? targetFramework,
        VerbosityOptions verbosity,
        bool lockPackageDownloads,
        CancellationToken cancellationToken)
    {
        await DownloadPackageIfNeededAsync(
            packageDownloadDir,
            packageId,
            packageVersion,
            nugetPackageDownloader,
            packageSourceLocation,
            includeUnlisted: givenSpecificVersion,
            verbosity,
            lockPackageDownloads,
            cancellationToken).ConfigureAwait(false);

        CreateAssetFile(packageId, packageVersion, packageDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.AssetsFileName), _runtimeJsonPath, verbosity, targetFramework);

        //  Also download RID-specific package if needed
        if (ResolveRidSpecificPackage(packageId, packageVersion, packageDownloadDir, assetFileDirectory, verbosity) is PackageIdentity ridSpecificPackage)
        {
            PackageId ridSpecificPackageId = new(ridSpecificPackage.Id);
            NuGetVersion ridSpecificPackageVersion = ridSpecificPackage.Version ?? packageVersion;
            await DownloadPackageIfNeededAsync(
                packageDownloadDir,
                ridSpecificPackageId,
                ridSpecificPackageVersion,
                nugetPackageDownloader,
                packageSourceLocation,
                includeUnlisted: true,
                verbosity,
                lockPackageDownloads,
                cancellationToken).ConfigureAwait(false);

            CreateAssetFile(ridSpecificPackageId, ridSpecificPackageVersion, packageDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.RidSpecificPackageAssetsFileName), _runtimeJsonPath, verbosity, targetFramework);
        }
    }

    private async Task DownloadPackageIfNeededAsync(
        DirectoryPath packageDownloadDir,
        PackageId packageId,
        NuGetVersion packageVersion,
        INuGetPackageDownloader nugetPackageDownloader,
        PackageSourceLocation packageSourceLocation,
        bool includeUnlisted,
        VerbosityOptions verbosity,
        bool lockPackageDownload,
        CancellationToken cancellationToken)
    {
        bool packageDownloaded = false;

        async Task<bool> DownloadAsync()
        {
            if (!IsPackageInstalled(packageId, packageVersion, packageDownloadDir.Value))
            {
                await DownloadAndExtractPackageAsync(
                    packageId,
                    nugetPackageDownloader,
                    packageDownloadDir.Value,
                    packageVersion,
                    packageSourceLocation,
                    verbosity,
                    includeUnlisted,
                    cancellationToken).ConfigureAwait(false);
                packageDownloaded = true;
            }

            return true;
        }

        if (lockPackageDownload)
        {
            await ExecuteWithToolInstallLockAsync(
                packageDownloadDir,
                packageId,
                packageVersion,
                cancellationToken,
                DownloadAsync,
                rollbackOnTransaction: () =>
                {
                    if (!packageDownloaded)
                    {
                        return null;
                    }

                    string packageDirectory = new VersionFolderPathResolver(packageDownloadDir.Value)
                        .GetInstallPath(packageId.ToString(), packageVersion);
                    return () =>
                    {
                        if (_fileSystem.Directory.Exists(packageDirectory))
                        {
                            _fileSystem.Directory.Delete(packageDirectory, true);
                        }
                    };
                }).ConfigureAwait(false);
        }
        else
        {
            await DownloadAsync().ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteWithToolInstallLockAsync<T>(
        DirectoryPath packageInstallRoot,
        PackageId packageId,
        NuGetVersion packageVersion,
        CancellationToken cancellationToken,
        Func<Task<T>> action,
        Func<Action?>? rollbackOnTransaction = null)
    {
        return await ExecuteWithToolInstallLockAsync(
            packageInstallRoot,
            packageId,
            packageVersion,
            packageVersion.ToNormalizedString(),
            cancellationToken,
            action,
            _lockInitialWaitTimeout,
            _lockWaitTimeout,
            rollbackOnTransaction).ConfigureAwait(false);
    }

    internal static async Task<T> ExecuteWithToolInstallLockAsync<T>(
        DirectoryPath packageInstallRoot,
        PackageId packageId,
        NuGetVersion? packageVersion,
        string packageVersionDisplay,
        CancellationToken cancellationToken,
        Func<Task<T>> action,
        TimeSpan? lockInitialWaitTimeout = null,
        TimeSpan? lockWaitTimeout = null,
        Func<Action?>? rollbackOnTransaction = null)
    {
        return await ExecuteWithToolInstallLockAsync(
            GetToolInstallLockFilePath(packageInstallRoot, packageId, packageVersion),
            packageId,
            packageVersionDisplay,
            cancellationToken,
            action,
            lockInitialWaitTimeout,
            lockWaitTimeout,
            rollbackOnTransaction,
            Transaction.Current).ConfigureAwait(false);
    }

    internal static async Task<T> ExecuteWithToolInstallStoreLockAsync<T>(
        DirectoryPath packageInstallRoot,
        PackageId packageId,
        string packageVersionDisplay,
        CancellationToken cancellationToken,
        Func<Task<T>> action,
        TimeSpan? lockInitialWaitTimeout = null,
        TimeSpan? lockWaitTimeout = null)
    {
        return await ExecuteWithToolInstallLockAsync(
            GetToolInstallStoreLockFilePath(packageInstallRoot),
            packageId,
            packageVersionDisplay,
            cancellationToken,
            action,
            lockInitialWaitTimeout,
            lockWaitTimeout,
            rollbackOnTransaction: null,
            Transaction.Current).ConfigureAwait(false);
    }

    private static async Task<T> ExecuteWithToolInstallLockAsync<T>(
        string lockFilePath,
        PackageId packageId,
        string packageVersionDisplay,
        CancellationToken cancellationToken,
        Func<Task<T>> action,
        TimeSpan? lockInitialWaitTimeout,
        TimeSpan? lockWaitTimeout,
        Func<Action?>? rollbackOnTransaction,
        Transaction? transaction)
    {
        string? transactionLockKey = transaction is null
            ? null
            : $"{transaction.TransactionInformation.LocalIdentifier}\0{Path.GetFullPath(lockFilePath)}";
        Func<Action?> transactionRollback = rollbackOnTransaction ?? (() => null);

        if (TryGetTransactionLock(transactionLockKey, out ToolPackageLockEnlistment? transactionLock))
        {
            return await transactionLock.ExecuteAsync(
                action,
                transactionRollback,
                cancellationToken).ConfigureAwait(false);
        }

        FileStream? lockFile = await TryAcquireToolInstallLockAsync(
            lockFilePath,
            lockInitialWaitTimeout ?? TimeSpan.FromMilliseconds(50),
            cancellationToken,
            () => transactionLockKey is not null && s_transactionLocks.ContainsKey(transactionLockKey)).ConfigureAwait(false);

        try
        {
            if (lockFile is null)
            {
                if (TryGetTransactionLock(transactionLockKey, out transactionLock))
                {
                    return await transactionLock.ExecuteAsync(
                        action,
                        transactionRollback,
                        cancellationToken).ConfigureAwait(false);
                }

                Reporter.Error.WriteLine(string.Format(CliStrings.ToolInstallationWaiting, packageId, packageVersionDisplay));

                lockFile = await TryAcquireToolInstallLockAsync(
                    lockFilePath,
                    lockWaitTimeout ?? TimeSpan.FromMinutes(5),
                    cancellationToken,
                    () => transactionLockKey is not null && s_transactionLocks.ContainsKey(transactionLockKey)).ConfigureAwait(false);
                if (lockFile is null)
                {
                    if (TryGetTransactionLock(transactionLockKey, out transactionLock))
                    {
                        return await transactionLock.ExecuteAsync(
                            action,
                            transactionRollback,
                            cancellationToken).ConfigureAwait(false);
                    }

                    throw new ToolPackageException(string.Format(CliStrings.ToolInstallationTimeout, packageId, packageVersionDisplay));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (transaction is not null)
            {
                transactionLock = new ToolPackageLockEnlistment(lockFile, transactionLockKey!);
                transaction.EnlistVolatile(transactionLock, EnlistmentOptions.None);
                if (!s_transactionLocks.TryAdd(transactionLockKey!, transactionLock))
                {
                    throw new InvalidOperationException("The transaction already owns this tool package lock.");
                }

                lockFile = null;
                return await transactionLock.ExecuteAsync(
                    action,
                    transactionRollback,
                    cancellationToken).ConfigureAwait(false);
            }

            return await action().ConfigureAwait(false);
        }
        finally
        {
            lockFile?.Dispose();
        }
    }

    private static async Task<FileStream?> TryAcquireToolInstallLockAsync(
        string lockFilePath,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<bool>? lockOwnedByTransaction = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            if (lockOwnedByTransaction?.Invoke() == true)
            {
                return null;
            }

            try
            {
                return new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException exception) when (IsToolInstallLockContention(exception))
            {
                if (lockOwnedByTransaction?.Invoke() == true)
                {
                    return null;
                }

                TimeSpan remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return null;
                }

                TimeSpan delay = TimeSpan.FromMilliseconds(
                    Math.Min(s_lockRetryDelay.TotalMilliseconds, remaining.TotalMilliseconds));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryGetTransactionLock(
        string? transactionLockKey,
        [NotNullWhen(true)] out ToolPackageLockEnlistment? transactionLock)
    {
        if (transactionLockKey is not null &&
            s_transactionLocks.TryGetValue(transactionLockKey, out transactionLock))
        {
            return true;
        }

        transactionLock = null;
        return false;
    }

    internal static string GetToolInstallLockFilePath(
        DirectoryPath packageInstallRoot,
        PackageId packageId,
        NuGetVersion? packageVersion)
    {
        string normalizedVersion = packageVersion?.ToNormalizedString().ToLowerInvariant() ?? string.Empty;
        string lockIdentity = $"package\n{packageId.ToString().ToLowerInvariant()}\n{normalizedVersion}";
        return GetToolInstallLockFilePath(packageInstallRoot, lockIdentity);
    }

    internal static string GetToolInstallStoreLockFilePath(DirectoryPath packageInstallRoot)
    {
        return GetToolInstallLockFilePath(packageInstallRoot, "store");
    }

    private static string GetToolInstallLockFilePath(DirectoryPath packageInstallRoot, string lockIdentity)
    {
        string lockFileName = $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockIdentity)))}.lock";
        return Path.Combine(Path.GetFullPath(packageInstallRoot.Value), ToolPackageStoreAndQuery.LockDirectory, lockFileName);
    }

    internal static bool IsToolInstallLockContention(IOException exception) =>
        OperatingSystem.IsWindows()
            ? exception.HResult is WindowsSharingViolationHResult or WindowsLockViolationHResult
            : exception.HResult is LinuxWouldBlockError or MacOSWouldBlockError;

    public Task<IToolPackage?> TryGetDownloadedToolAsync(
        PackageId packageId,
        NuGetVersion packageVersion,
        string? targetFramework,
        VerbosityOptions verbosity,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithToolInstallLockAsync(
            _localToolDownloadDir,
            packageId,
            packageVersion,
            cancellationToken,
            () => Task.FromResult(GetDownloadedTool()));

        IToolPackage? GetDownloadedTool()
        {
            if (!IsPackageInstalled(packageId, packageVersion, _localToolDownloadDir.Value))
            {
                return null;
            }

            DirectoryPath assetFileDirectory = GetLocalToolAssetDirectory(packageId, packageVersion);
            _fileSystem.Directory.CreateDirectory(assetFileDirectory.Value);
            CreateAssetFile(packageId, packageVersion, _localToolDownloadDir, Path.Combine(assetFileDirectory.Value, ToolPackageInstance.AssetsFileName), _runtimeJsonPath, verbosity, targetFramework);

            if (ResolveRidSpecificPackage(packageId, packageVersion, _localToolDownloadDir, assetFileDirectory, verbosity) is PackageIdentity ridSpecificPackage)
            {
                PackageId ridSpecificPackageId = new(ridSpecificPackage.Id);
                NuGetVersion ridSpecificPackageVersion = ridSpecificPackage.Version ?? packageVersion;
                if (!IsPackageInstalled(ridSpecificPackageId, ridSpecificPackageVersion, _localToolDownloadDir.Value))
                {
                    return null;
                }

                CreateAssetFile(ridSpecificPackageId, ridSpecificPackageVersion, _localToolDownloadDir,
                    Path.Combine(assetFileDirectory.Value, ToolPackageInstance.RidSpecificPackageAssetsFileName), _runtimeJsonPath, verbosity, targetFramework);
            }

            return new ToolPackageInstance(
                id: packageId,
                version: packageVersion,
                packageDirectory: _localToolDownloadDir,
                assetsJsonParentDirectory: assetFileDirectory,
                fileSystem: _fileSystem);
        }
    }

    private DirectoryPath GetLocalToolAssetDirectory(PackageId packageId, NuGetVersion packageVersion) =>
        _localToolAssetDir.WithSubDirectories(
            packageId.ToString().ToLowerInvariant(),
            packageVersion.ToNormalizedString().ToLowerInvariant());

    private PackageIdentity? ResolveRidSpecificPackage(PackageId packageId,
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
            return resolvedPackage;
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

    public virtual async Task<(NuGetVersion version, PackageSource source)> GetNuGetVersionAsync(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default)
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

        return await nugetPackageDownloader
            .GetBestPackageVersionAndSourceAsync(packageId, versionRange, packageSourceLocation, cancellationToken)
            .ConfigureAwait(false);
    }
}
