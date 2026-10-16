// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Security.Cryptography;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>
/// Installs a .NET component by downloading its official Windows installer bundle (.exe), verifying it,
/// and running it. The bundle owns the destination (the shared Program Files folder), so dotnetup
/// lays down no files itself and tracks no subcomponents.
/// </summary>
internal sealed class WindowsBundleArtifact : IInstallArtifact
{
    /// <summary>Quiet, per-machine install that never restarts the computer.</summary>
    internal static IReadOnlyList<string> InstallArguments { get; } = ["/install", "/quiet", "/norestart"];

    private readonly DotnetInstallRequest _request;
    private readonly ReleaseVersion _version;
    private readonly IArchiveDownloader _downloader;
    private readonly IInstallerSignatureVerifier _signatureVerifier;
    private readonly IBundleProcessRunner _processRunner;
    private readonly ExtractorProgressTracker _progress;
    private readonly InstallBatchContext _batchContext;
    private readonly TextWriter _notices;
    private VerifiedDownload? _download;

    // Denies write and delete access from the moment the bundle is verified until this artifact is
    // disposed, so the file that was verified is the file that runs.
    private FileStream? _lockedBundle;

    public WindowsBundleArtifact(
        DotnetInstallRequest request,
        ReleaseVersion version,
        InstallBatchContext batchContext,
        IArchiveDownloader? downloader = null,
        IInstallerSignatureVerifier? signatureVerifier = null,
        IBundleProcessRunner? processRunner = null,
        TextWriter? notices = null,
        ReleaseManifest? releaseManifest = null,
        bool? isPrivilegedProcess = null)
    {
        _request = request;
        _version = version;
        _signatureVerifier = signatureVerifier ?? CreateDefaultSignatureVerifier();
        _processRunner = processRunner ?? new BundleProcessRunner();
        _notices = notices ?? Console.Error;
        _batchContext = batchContext;
        _progress = new ExtractorProgressTracker(batchContext.Reporter, request.Component, version.ToString(), batchContext.VersionDisplayWidth);

        UsesSecureScratchDirectory = OperatingSystem.IsWindows() && (isPrivilegedProcess ?? Environment.IsPrivilegedProcess);
        ScratchDirectory = CreateScratchDirectory();

        // An elevated run keeps its cache inside the admin-only scratch directory. The per-user cache is writable
        // without elevation, and serving from it would hard-link a user-writable file into the secure directory.
        _downloader = downloader ?? new DotnetDownloader(
            releaseManifest ?? ReleaseManifest.Default,
            cacheDirectory: UsesSecureScratchDirectory ? Path.Combine(ScratchDirectory, "downloadcache") : null);
    }

    /// <summary>
    /// Download directory for the bundle. Exposed for testing. Without elevation it is a new directory under the
    /// per-user temp directory: other users cannot reach it, but it inherits the user's profile ACL, so any process
    /// running as the same user can write to it. In an elevated process it is created by
    /// <see cref="SecureScratchDirectory"/>, so only SYSTEM and Administrators can modify it. In both cases the lock
    /// taken in <see cref="Prepare"/> protects the bundle itself.
    /// </summary>
    internal string ScratchDirectory { get; }

    /// <summary>Whether <see cref="ScratchDirectory"/> is admin-only. Exposed for testing.</summary>
    internal bool UsesSecureScratchDirectory { get; }

    /// <summary>The downloaded bundle path once <see cref="Prepare"/> has completed. Exposed for testing.</summary>
    internal string? BundlePath => _download?.Path;

    public IReadOnlyList<string> ExtractedSubcomponents => [];

    public void Prepare()
    {
        var (reporter, downloadTask) = _progress.BeginDownload();
        string basePath = Path.Combine(ScratchDirectory, $"{BlobFeedUrlBuilder.GetArchivePrefix(_request.Component)}-{Guid.NewGuid():N}");
        VerifiedDownload download = Download(basePath, reporter);
        EnsureInstallerExtension(download.Path);

        FileStream lockedBundle = OpenLocked(download.Path);
        try
        {
            EnsureMatchesVerifiedHash(lockedBundle, download.ExpectedHash);
        }
        catch
        {
            lockedBundle.Dispose();
            throw;
        }

        _lockedBundle = lockedBundle;
        _download = download;
        _progress.CompleteDownload(downloadTask, download.Path);
    }

    public void Commit()
    {
        VerifiedDownload download = _download ?? throw new InvalidOperationException("Prepare() must be called before Commit().");
        var installTask = _progress.BeginExtraction();

        EnsureTrustedSignature(download);
        HandleExitCode(RunInstaller(download.Path));

        installTask.Value = installTask.MaxValue;
        _progress.CompleteExtraction(installTask);
    }

    public void Dispose()
    {
        _lockedBundle?.Dispose();
        _lockedBundle = null;
        try
        {
            if (Directory.Exists(ScratchDirectory))
            {
                Directory.Delete(ScratchDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static IInstallerSignatureVerifier CreateDefaultSignatureVerifier() =>
        OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)
            ? new AuthenticodeInstallerSignatureVerifier()
            : UnsupportedPlatformInstallerSignatureVerifier.Instance;

    private string CreateScratchDirectory()
    {
        if (!UsesSecureScratchDirectory || !OperatingSystem.IsWindows())
        {
            return Directory.CreateTempSubdirectory().FullName;
        }

        try
        {
            return SecureScratchDirectory.Create();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw CreateException(
                DotnetInstallErrorCode.InstallerFailed,
                $"Could not create a secure download directory for {Describe()}: {ex.Message}",
                ex);
        }
    }

    private VerifiedDownload Download(string basePath, IProgress<DownloadProgress>? reporter)
    {
        try
        {
            return _downloader.DownloadVerifiedArtifact(_request, _version, basePath, reporter);
        }
        catch (DotnetInstallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateException(
                DotnetInstallErrorCode.DownloadFailed,
                $"Failed to download the .NET installer for version {_version}: {ex.Message}",
                ex);
        }
    }

    private FileStream OpenLocked(string bundlePath)
    {
        try
        {
            return new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CreateException(
                DotnetInstallErrorCode.InstallerFailed,
                $"Could not open the downloaded installer for {Describe()}: {ex.Message}",
                ex);
        }
    }

    private void EnsureInstallerExtension(string bundlePath)
    {
        if (!bundlePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateException(
                DotnetInstallErrorCode.InstallerFailed,
                $"Expected a Windows installer (.exe) for {Describe()}, but the download resolved to '{Path.GetFileName(bundlePath)}'.");
        }
    }

    /// <summary>
    /// Re-hashes the bundle through the locked handle and compares it with the hash the downloader verified,
    /// so a file swapped on disk after the download (in the scratch directory or the hard-linked download
    /// cache) is rejected.
    /// </summary>
    private void EnsureMatchesVerifiedHash(FileStream lockedBundle, string expectedHash)
    {
        if (!DotnetDownloader.IsAcceptedArtifactHash(ComputeHash(lockedBundle), expectedHash))
        {
            throw CreateException(
                DotnetInstallErrorCode.HashMismatch,
                $"The downloaded installer for {Describe()} does not match its expected hash; it may have been modified after it was downloaded. Run the command again.");
        }
    }

    private void EnsureTrustedSignature(VerifiedDownload download)
    {
        InstallerSignatureResult result = _signatureVerifier.Verify(download.Path);
        if (result.IsTrusted || IsUnsignedSourceAllowed(download))
        {
            return;
        }

        throw CreateException(
            DotnetInstallErrorCode.SignatureVerificationFailed,
            $"The installer for {Describe()} failed Authenticode signature verification: {result.FailureReason}");
    }

    /// <summary>
    /// Installers from the blob feed are not Authenticode signed. Their SHA-512 hash was verified during
    /// download and the user was warned up front, so they may run unless IT policy blocks unsigned downloads.
    /// Installers resolved from the signed releases manifest must always pass Authenticode, whatever the channel.
    /// </summary>
    private static bool IsUnsignedSourceAllowed(VerifiedDownload download) =>
        download.IsUnsigned && !UnsignedSourcePolicy.IsUnsignedDownloadBlocked();

    private int RunInstaller(string bundlePath)
    {
        try
        {
            return _processRunner.Run(bundlePath, InstallArguments, _batchContext.RequestCancellation);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or PlatformNotSupportedException)
        {
            throw CreateException(
                DotnetInstallErrorCode.ProcessStartFailed,
                $"Could not start the installer for {Describe()}: {ex.Message}",
                ex);
        }
    }

    private void HandleExitCode(int exitCode)
    {
        BundleExitOutcome outcome = BundleExitCodes.Classify(exitCode);
        if (outcome == BundleExitOutcome.Success)
        {
            return;
        }

        if (outcome == BundleExitOutcome.SuccessRestartRequired)
        {
            _notices.WriteLine($"A restart is required to finish installing {Describe()}. dotnetup will not restart your computer; restart it when convenient.");
            return;
        }

        throw CreateExitCodeException(outcome, exitCode);
    }

    private DotnetInstallException CreateExitCodeException(BundleExitOutcome outcome, int exitCode) => outcome switch
    {
        BundleExitOutcome.Cancelled => CreateException(
            DotnetInstallErrorCode.OperationCancelled,
            $"The installation of {Describe()} was cancelled (installer exit code {exitCode})."),
        BundleExitOutcome.Busy => CreateException(
            DotnetInstallErrorCode.InstallerBusy,
            $"Another installation is already in progress (installer exit code {exitCode}). Wait for it to finish, then run the command again."),
        BundleExitOutcome.ElevationRequired => CreateException(
            DotnetInstallErrorCode.InstallerFailed,
            $"The installer for {Describe()} requires administrator privileges but could not elevate (installer exit code {exitCode}). Run the command from an account that can approve the administrator (UAC) prompt."),
        _ => CreateException(
            DotnetInstallErrorCode.InstallerFailed,
            $"The installer for {Describe()} failed with exit code {exitCode} (0x{exitCode:X8})."),
    };

    private string Describe() => $"{_request.Component.GetDisplayName()} {_version}";

    private DotnetInstallException CreateException(DotnetInstallErrorCode errorCode, string message, Exception? innerException = null) =>
        innerException is null
            ? new(errorCode, message, version: _version.ToString(), component: _request.Component.ToString())
            : new(errorCode, message, innerException, version: _version.ToString(), component: _request.Component.ToString());

    private static string ComputeHash(Stream stream)
    {
        stream.Position = 0;
        return Convert.ToHexStringLower(SHA512.HashData(stream));
    }
}
