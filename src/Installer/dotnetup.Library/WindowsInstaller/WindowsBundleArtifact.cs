// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
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
        DotnetupPolicy? policy = null)
    {
        // Installer bundles are only trusted through the signed releases.json manifest.
        _request = request with { Options = request.Options with { RequireReleasesJsonSignature = true } };
        _version = version;
        _signatureVerifier = signatureVerifier ?? CreateDefaultSignatureVerifier();
        _processRunner = processRunner ?? new BundleProcessRunner();
        _notices = notices ?? Console.Error;
        _batchContext = batchContext;
        _progress = new ExtractorProgressTracker(batchContext.Reporter, request.Component, version.ToString(), batchContext.VersionDisplayWidth);

        ScratchDirectory = Directory.CreateTempSubdirectory().FullName;
        _downloader = downloader ?? new DotnetDownloader(releaseManifest ?? ReleaseManifest.Default, policy: policy);
    }

    /// <summary>
    /// Download directory for the bundle. Exposed for testing. The lock taken in <see cref="Prepare"/> protects the
    /// bundle itself until installation finishes.
    /// </summary>
    internal string ScratchDirectory { get; }

    /// <summary>The downloaded bundle path once <see cref="Prepare"/> has completed. Exposed for testing.</summary>
    internal string? BundlePath => _download?.Path;

    public bool RequiresPostInstallValidation { get; private set; } = true;

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

        EnsureMicrosoftAuthenticodeSignature(download);
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
                Format(Strings.WindowsInstallerDownloadFailed, _version, ex.Message),
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
                Format(Strings.WindowsInstallerOpenFailed, Describe(), ex.Message),
                ex);
        }
    }

    private void EnsureInstallerExtension(string bundlePath)
    {
        if (!bundlePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateException(
                DotnetInstallErrorCode.WrongInstallerFileExtension,
                Format(Strings.WindowsInstallerWrongFileExtension, Describe(), Path.GetFileName(bundlePath)));
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
                Format(Strings.WindowsInstallerHashMismatch, Describe()));
        }
    }

    private void EnsureMicrosoftAuthenticodeSignature(VerifiedDownload download)
    {
        InstallerSignatureResult result = _signatureVerifier.Verify(download.Path);
        if (result.IsTrusted)
        {
            return;
        }

        throw CreateException(
            DotnetInstallErrorCode.SignatureVerificationFailed,
            Format(Strings.WindowsInstallerSignatureVerificationFailed, Describe(), result.FailureReason));
    }

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
                Format(Strings.WindowsInstallerProcessStartFailed, Describe(), ex.Message),
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
            RequiresPostInstallValidation = false;
            _notices.WriteLine(Format(Strings.WindowsInstallerRestartRequired, Describe()));
            return;
        }

        if (outcome == BundleExitOutcome.SuccessRestartInitiated)
        {
            RequiresPostInstallValidation = false;
            _notices.WriteLine(Format(Strings.WindowsInstallerRestartInitiated, Describe()));
            return;
        }

        throw CreateExitCodeException(outcome, exitCode);
    }

    private DotnetInstallException CreateExitCodeException(BundleExitOutcome outcome, int exitCode) => outcome switch
    {
        BundleExitOutcome.Cancelled => CreateException(
            DotnetInstallErrorCode.OperationCancelled,
            Format(Strings.WindowsInstallerCancelled, Describe(), exitCode)),
        BundleExitOutcome.Busy => CreateException(
            DotnetInstallErrorCode.InstallerBusy,
            Format(Strings.WindowsInstallerBusy, exitCode)),
        BundleExitOutcome.ElevationRequired => CreateException(
            DotnetInstallErrorCode.InstallerFailed,
            Format(Strings.WindowsInstallerElevationRequired, Describe(), exitCode)),
        _ => CreateException(
            DotnetInstallErrorCode.InstallerFailed,
            Format(Strings.WindowsInstallerFailed, Describe(), exitCode, exitCode)),
    };

    private string Describe() => $"{_request.Component.GetDisplayName()} {_version}";

    private static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

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
