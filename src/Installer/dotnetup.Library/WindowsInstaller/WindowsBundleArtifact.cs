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
    private readonly TextWriter _notices;
    private string? _bundlePath;
    private string? _downloadedHash;

    public WindowsBundleArtifact(
        DotnetInstallRequest request,
        ReleaseVersion version,
        InstallBatchContext batchContext,
        IArchiveDownloader? downloader = null,
        IInstallerSignatureVerifier? signatureVerifier = null,
        IBundleProcessRunner? processRunner = null,
        TextWriter? notices = null)
    {
        _request = request;
        _version = version;
        _downloader = downloader ?? new DotnetDownloader(ReleaseManifest.Default, cacheDirectory: DotnetupPaths.DownloadCacheDirectory);
        _signatureVerifier = signatureVerifier ?? new AuthenticodeInstallerSignatureVerifier();
        _processRunner = processRunner ?? new BundleProcessRunner();
        _notices = notices ?? Console.Error;
        _progress = new ExtractorProgressTracker(batchContext.Reporter, request.Component, version.ToString(), batchContext.VersionDisplayWidth);
        ScratchDirectory = Directory.CreateTempSubdirectory().FullName;
    }

    /// <summary>Private download directory for the bundle. Exposed for testing.</summary>
    internal string ScratchDirectory { get; }

    /// <summary>The downloaded bundle path once <see cref="Prepare"/> has completed. Exposed for testing.</summary>
    internal string? BundlePath => _bundlePath;

    public IReadOnlyList<string> ExtractedSubcomponents => [];

    public void Prepare()
    {
        var (reporter, downloadTask) = _progress.BeginDownload();
        string basePath = Path.Combine(ScratchDirectory, $"{BlobFeedUrlBuilder.GetArchivePrefix(_request.Component)}-{Guid.NewGuid():N}");
        string bundlePath = Download(basePath, reporter);

        if (!bundlePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateException(
                DotnetInstallErrorCode.InstallerFailed,
                $"Expected a Windows installer (.exe) for {Describe()}, but the download resolved to '{Path.GetFileName(bundlePath)}'.");
        }

        using (var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _downloadedHash = ComputeHash(stream);
        }

        _bundlePath = bundlePath;
        _progress.CompleteDownload(downloadTask, bundlePath);
    }

    public void Commit()
    {
        string bundlePath = _bundlePath ?? throw new InvalidOperationException("Prepare() must be called before Commit().");
        var installTask = _progress.BeginExtraction();

        // Hold a handle that denies write and delete access until the installer exits, so the file that
        // was verified is the file that runs.
        using (var lockedBundle = OpenLocked(bundlePath))
        {
            EnsureUnchangedSinceDownload(lockedBundle);
            EnsureTrustedSignature(bundlePath);
            HandleExitCode(RunInstaller(bundlePath));
        }

        installTask.Value = installTask.MaxValue;
        _progress.CompleteExtraction(installTask);
    }

    public void Dispose()
    {
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

    private string Download(string basePath, IProgress<DownloadProgress>? reporter)
    {
        try
        {
            return _downloader.DownloadArchiveWithVerification(_request, _version, basePath, reporter);
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

    private void EnsureUnchangedSinceDownload(FileStream lockedBundle)
    {
        if (!string.Equals(ComputeHash(lockedBundle), _downloadedHash, StringComparison.Ordinal))
        {
            throw CreateException(
                DotnetInstallErrorCode.HashMismatch,
                $"The downloaded installer for {Describe()} was modified after it was verified. Run the command again.");
        }
    }

    private void EnsureTrustedSignature(string bundlePath)
    {
        InstallerSignatureResult result = _signatureVerifier.Verify(bundlePath);
        if (result.IsTrusted || IsUnsignedSourceAllowed())
        {
            return;
        }

        throw CreateException(
            DotnetInstallErrorCode.SignatureVerificationFailed,
            $"The installer for {Describe()} failed Authenticode signature verification: {result.FailureReason}");
    }

    /// <summary>
    /// Daily and unreleased prerelease builds come from the blob feed, whose installers are not Authenticode
    /// signed. Their SHA-512 hash was verified during download and the user was warned up front, so they
    /// may run unless IT policy blocks unsigned downloads.
    /// </summary>
    private bool IsUnsignedSourceAllowed() =>
        UnsignedSourcePolicy.MayDownloadUnsigned(_request) && !UnsignedSourcePolicy.IsUnsignedDownloadBlocked();

    private int RunInstaller(string bundlePath)
    {
        try
        {
            return _processRunner.Run(bundlePath, InstallArguments);
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
