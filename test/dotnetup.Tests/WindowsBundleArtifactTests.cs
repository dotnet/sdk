// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for <see cref="WindowsBundleArtifact"/> using fake downloader, signature verifier, and process
/// runner. No installer is ever run.
/// </summary>
[TestClass]
public class WindowsBundleArtifactTests : IDisposable
{
    private const string StableChannel = "10.0.100";
    private readonly LazyProgressReporter _reporter = new(new NullProgressTarget());
    private readonly List<WindowsBundleArtifact> _artifacts = [];
    private readonly FakeDotnetupPolicyStore _policyStore = new();

    public void Dispose()
    {
        foreach (var artifact in _artifacts)
        {
            artifact.Dispose();
        }

        _reporter.Dispose();
    }

    [TestMethod]
    public void Prepare_DownloadsBundleIntoScratchDirectory()
    {
        var downloader = CreateDownloader();
        var artifact = CreateArtifact(StableChannel, downloader: downloader);

        artifact.Prepare();

        downloader.DownloadCalls.Should().ContainSingle();
        artifact.BundlePath.Should().StartWith(artifact.ScratchDirectory).And.EndWith(".exe");
        File.Exists(artifact.BundlePath).Should().BeTrue();
        artifact.ExtractedSubcomponents.Should().BeEmpty();
    }

    [TestMethod]
    public void Prepare_RequestWithoutExpectation_StillExpectsReleasesJsonSignature()
    {
        var downloader = CreateDownloader();
        var artifact = CreateArtifact(StableChannel, downloader: downloader);

        artifact.Prepare();

        downloader.DownloadCalls.Should().ContainSingle()
            .Which.Request.Options.ExpectReleasesJsonSignature.Should().BeTrue();
    }

    [TestMethod]
    public void Prepare_NonExeDownload_ThrowsWrongInstallerFileExtension()
    {
        var downloader = CreateDownloader();
        downloader.ArchiveFileExtension = ".zip";
        var artifact = CreateArtifact(StableChannel, downloader: downloader);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Prepare);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.WrongInstallerFileExtension);
        ex.Message.Should().Contain(".exe");
    }

    [TestMethod]
    public void Commit_RunsBundleQuietlyWithoutRestart()
    {
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(StableChannel, runner: runner);
        artifact.Prepare();

        artifact.Commit();

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Path.Should().Be(artifact.BundlePath);
        runner.Calls[0].Arguments.Should().Equal("/install", "/quiet", "/norestart");
    }

    [TestMethod]
    public void Commit_SignatureFailureOnSignedSource_ThrowsAndDoesNotRunInstaller()
    {
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(StableChannel, verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
        ex.Message.Should().Be($"The installer for .NET SDK 10.0.100 failed Authenticode signature verification: {FakeVerifier.UntrustedReason}");
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("daily")]
    [DataRow("10.0.100-preview.4.25216.37")]
    public void Commit_SignatureFailureOnSignedSourceForPrereleaseChannel_ThrowsAndDoesNotRunInstaller(string channel)
    {
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(channel, downloader: CreateDownloader(isUnsigned: false), verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("daily")]
    [DataRow("10.0.100-preview.4.25216.37")]
    [DataRow(StableChannel)]
    public void Commit_UnsignedSourceAllowedByPolicy_ThrowsAndDoesNotRunInstaller(string channel)
    {
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(channel, downloader: CreateDownloader(isUnsigned: true), verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        var exception = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        exception.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
        exception.Message.Should().Be($"The installer for .NET SDK 10.0.100 failed Authenticode signature verification: {FakeVerifier.UntrustedReason}");
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void Commit_UnsignedSourceBlockedByPolicy_ThrowsAndDoesNotRunInstaller()
    {
        _policyStore.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact("daily", downloader: CreateDownloader(isUnsigned: true), verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void Prepare_BundleReplacedAfterDownloadVerification_ThrowsHashMismatchAndReleasesFile()
    {
        var downloader = CreateDownloader();
        downloader.ExpectedHash = Convert.ToHexStringLower(SHA512.HashData(downloader.FakeArchiveContent));
        downloader.FakeArchiveContent = [0x4D, 0x5A, 9, 9, 9];
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(StableChannel, downloader: downloader, runner: runner);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Prepare);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.HashMismatch);
        artifact.BundlePath.Should().BeNull();
        Assert.ThrowsExactly<InvalidOperationException>(artifact.Commit);
        runner.Calls.Should().BeEmpty();
        File.Delete(downloader.DownloadCalls[0].DestinationPath);
    }

    [TestMethod]
    public void Prepare_AcceptsExpectedHashInAnyCase()
    {
        var downloader = CreateDownloader();
        downloader.ExpectedHash = Convert.ToHexString(SHA512.HashData(downloader.FakeArchiveContent));
        var artifact = CreateArtifact(StableChannel, downloader: downloader);

        artifact.Prepare();

        artifact.BundlePath.Should().NotBeNull();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Prepare_LocksBundleAgainstWriteDeleteAndRenameUntilDisposed()
    {
        var artifact = CreateArtifact(StableChannel);
        artifact.Prepare();
        string bundlePath = artifact.BundlePath!;

        Record.Exception(() => File.Delete(bundlePath)).Should().Match<Exception>(e => e is IOException || e is UnauthorizedAccessException);
        Record.Exception(() => File.OpenWrite(bundlePath).Dispose()).Should().Match<Exception>(e => e is IOException || e is UnauthorizedAccessException);
        Record.Exception(() => File.Move(bundlePath, bundlePath + ".moved")).Should().Match<Exception>(e => e is IOException || e is UnauthorizedAccessException,
            "renaming the bundle would let another file take its place");
        Record.Exception(() => Directory.Move(artifact.ScratchDirectory, artifact.ScratchDirectory + "-moved")).Should().Match<Exception>(e => e is IOException || e is UnauthorizedAccessException,
            "renaming the scratch directory would let another directory take its place");
        File.ReadAllBytes(bundlePath).Should().NotBeEmpty("other readers such as the installer must still be able to read the bundle");

        artifact.Dispose();

        File.Exists(bundlePath).Should().BeFalse();
    }

    [TestMethod]
    public void Constructor_UsesPerUserTempDirectory()
    {
        var artifact = CreateArtifact(StableChannel);

        artifact.ScratchDirectory.Should().StartWith(Path.GetTempPath());
    }

    [TestMethod]
    public void Commit_CancelRequestedWhileInstallerRuns_ReportsInstallerResultAndFlagsBatch()
    {
        var batchContext = new InstallBatchContext(_reporter, 8);
        var runner = new FakeRunner(exitCode: 0) { RequestCancellation = true };
        var artifact = CreateArtifact(StableChannel, runner: runner, batchContext: batchContext);
        artifact.Prepare();

        artifact.Commit();

        runner.Calls.Should().ContainSingle();
        batchContext.IsCancellationRequested.Should().BeTrue();
    }

    [TestMethod]
    public void Commit_CancelRequestedAndInstallerFails_ReportsInstallerFailure()
    {
        var batchContext = new InstallBatchContext(_reporter, 8);
        var artifact = CreateArtifact(StableChannel, runner: new FakeRunner(exitCode: 1603) { RequestCancellation = true }, batchContext: batchContext);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallerFailed);
        batchContext.IsCancellationRequested.Should().BeTrue();
    }

    [TestMethod]
    public void ConsumePreparedInstalls_CancelRequestedDuringFirstInstaller_SkipsRemainingInstallers()
    {
        var batchContext = new InstallBatchContext(_reporter, 8);
        var firstRunner = new FakeRunner(exitCode: 0) { RequestCancellation = true };
        var secondRunner = new FakeRunner(exitCode: 0);
        var first = Prepare(CreateArtifact(StableChannel, runner: firstRunner, batchContext: batchContext), StableChannel);
        var second = Prepare(CreateArtifact("10.0.200", runner: secondRunner, batchContext: batchContext), "10.0.200");
        using var readyQueue = new System.Collections.Concurrent.BlockingCollection<InstallerOrchestratorSingleton.PreparedInstall>(
            new System.Collections.Concurrent.ConcurrentQueue<InstallerOrchestratorSingleton.PreparedInstall>([first, second]));
        readyQueue.CompleteAdding();
        var results = new List<InstallResult>();
        var failures = new List<InstallFailure>();
        var fatalExceptions = new List<Exception>();

        InstallerOrchestratorSingleton.ConsumeConcurrentPreparedInstallsAndCommit(
            readyQueue, batchContext, CommitWithoutValidation, results, failures, fatalExceptions, new object());

        results.Should().ContainSingle().Which.Install.Should().Be(first.Install);
        failures.Should().ContainSingle();
        failures[0].Request.Should().Be(second.ResolvedRequest);
        failures[0].Exception.ErrorCode.Should().Be(DotnetInstallErrorCode.OperationCancelled);
        fatalExceptions.Should().BeEmpty();
        firstRunner.Calls.Should().ContainSingle();
        secondRunner.Calls.Should().BeEmpty();
        Directory.Exists(((WindowsBundleArtifact)second.Artifact).ScratchDirectory).Should().BeFalse("skipped installs are still disposed");
    }

    [TestMethod]
    [DataRow(0, null, true)]
    [DataRow(3010, "must restart", false)]
    [DataRow(1641, "restart has been initiated", false)]
    public void Commit_SuccessExitCodes_Succeed(int exitCode, string? expectedNotice, bool requiresPostInstallValidation)
    {
        using var notices = new StringWriter(CultureInfo.InvariantCulture);
        var artifact = CreateArtifact(StableChannel, runner: new FakeRunner(exitCode), notices: notices);
        artifact.Prepare();

        artifact.Commit();

        if (expectedNotice is not null)
        {
            notices.ToString().Should().Contain(expectedNotice);
        }
        else
        {
            notices.ToString().Should().BeEmpty();
        }

        artifact.RequiresPostInstallValidation.Should().Be(requiresPostInstallValidation);
    }

    [TestMethod]
    [DataRow(3010)]
    [DataRow(1641)]
    public void CommitPreparedInstall_RestartExitCodes_SkipPostInstallVerification(int exitCode)
    {
        var runner = new FakeRunner(exitCode);
        using var prepared = Prepare(CreateArtifact(StableChannel, runner: runner), StableChannel);
        var validator = new MockInstallationValidator(validateResult: false);

        var result = InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(prepared, validator);

        result.WasAlreadyInstalled.Should().BeFalse();
        runner.Calls.Should().ContainSingle();
        validator.ValidateCalls.Should().Be(1, "only the pre-install check should run while a restart is pending");
    }

    [TestMethod]
    [DataRow(1602, DotnetInstallErrorCode.OperationCancelled)]
    [DataRow(1223, DotnetInstallErrorCode.OperationCancelled)]
    [DataRow(1618, DotnetInstallErrorCode.InstallerBusy)]
    [DataRow(740, DotnetInstallErrorCode.InstallerFailed)]
    [DataRow(1603, DotnetInstallErrorCode.InstallerFailed)]
    [DataRow(-2147023293, DotnetInstallErrorCode.InstallerFailed)]
    public void Commit_FailureExitCodes_MapToErrorCodes(int exitCode, DotnetInstallErrorCode expected)
    {
        var artifact = CreateArtifact(StableChannel, runner: new FakeRunner(exitCode));
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(expected);
        ex.Message.Should().Contain(exitCode.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Commit_ProcessStartFailure_ThrowsProcessStartFailed()
    {
        var runner = new FakeRunner(exitCode: 0) { OnRun = _ => throw new Win32Exception(2) };
        var artifact = CreateArtifact(StableChannel, runner: runner);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.ProcessStartFailed);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Commit_BundleIsLockedAgainstWriteAndDeleteWhileInstallerRuns()
    {
        Exception? deleteFailure = null;
        Exception? writeFailure = null;
        var runner = new FakeRunner(exitCode: 0)
        {
            OnRun = path =>
            {
                deleteFailure = Record.Exception(() => File.Delete(path));
                writeFailure = Record.Exception(() => File.OpenWrite(path).Dispose());
                File.ReadAllBytes(path).Should().NotBeEmpty("the installer must still be able to read the bundle");
            },
        };
        var artifact = CreateArtifact(StableChannel, runner: runner);
        artifact.Prepare();

        artifact.Commit();

        deleteFailure.Should().Match<Exception>(e => e is IOException || e is UnauthorizedAccessException);
        writeFailure.Should().Match<Exception>(e => e is IOException || e is UnauthorizedAccessException);
        File.Exists(artifact.BundlePath).Should().BeTrue();
    }

    [TestMethod]
    public void Commit_WithoutPrepare_Throws()
    {
        var artifact = CreateArtifact(StableChannel);

        Assert.ThrowsExactly<InvalidOperationException>(artifact.Commit);
    }

    [TestMethod]
    public void Dispose_DeletesScratchDirectory()
    {
        var artifact = CreateArtifact(StableChannel);
        artifact.Prepare();
        Directory.Exists(artifact.ScratchDirectory).Should().BeTrue();

        artifact.Dispose();

        Directory.Exists(artifact.ScratchDirectory).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows5.1.2600")]
    public void AuthenticodeVerifier_RejectsUnsignedFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dotnetup-unsigned-{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(path, [0x4D, 0x5A, 0, 0]);
        try
        {
            var result = new AuthenticodeInstallerSignatureVerifier().Verify(path);

            result.IsTrusted.Should().BeFalse();
            result.FailureReason.Should().NotBeNullOrEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(0, BundleExitOutcome.Success)]
    [DataRow(3010, BundleExitOutcome.SuccessRestartRequired)]
    [DataRow(1641, BundleExitOutcome.SuccessRestartInitiated)]
    [DataRow(1602, BundleExitOutcome.Cancelled)]
    [DataRow(1223, BundleExitOutcome.Cancelled)]
    [DataRow(1618, BundleExitOutcome.Busy)]
    [DataRow(740, BundleExitOutcome.ElevationRequired)]
    [DataRow(1, BundleExitOutcome.Failed)]
    internal void BundleExitCodes_Classify(int exitCode, BundleExitOutcome expected)
    {
        BundleExitCodes.Classify(exitCode).Should().Be(expected);
    }

    private static MockArchiveDownloader CreateDownloader(bool isUnsigned = false) => new()
    {
        ArchiveFileExtension = ".exe",
        FakeArchiveContent = [0x4D, 0x5A, 1, 2, 3, 4],
        IsUnsigned = isUnsigned,
    };

    private static DotnetInstallRequest CreateRequest(string channel) => new(
        new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-bundle-root-unused"), InstallArchitecture.x64),
        new UpdateChannel(channel),
        InstallComponent.SDK,
        new InstallRequestOptions { Untracked = true, InstallMechanism = InstallMechanism.WindowsInstallerBundle });

    private static InstallerOrchestratorSingleton.PreparedInstall Prepare(WindowsBundleArtifact artifact, string channel)
    {
        artifact.Prepare();
        var resolvedRequest = new ResolvedInstallRequest(CreateRequest(channel), new ReleaseVersion("10.0.100"));
        var install = new DotnetInstall(resolvedRequest.Request.InstallRoot, resolvedRequest.ResolvedVersion, InstallComponent.SDK);
        return new InstallerOrchestratorSingleton.PreparedInstall(resolvedRequest, install, artifact);
    }

    private static InstallResult CommitWithoutValidation(InstallerOrchestratorSingleton.PreparedInstall prepared)
    {
        prepared.Artifact.Commit();
        return new InstallResult(prepared.Install, WasAlreadyInstalled: false);
    }

    private WindowsBundleArtifact CreateArtifact(
        string channel,
        MockArchiveDownloader? downloader = null,
        IInstallerSignatureVerifier? verifier = null,
        FakeRunner? runner = null,
        TextWriter? notices = null,
        InstallBatchContext? batchContext = null)
    {
        var artifact = new WindowsBundleArtifact(
            CreateRequest(channel),
            new ReleaseVersion("10.0.100"),
            batchContext ?? new InstallBatchContext(_reporter, 8),
            downloader ?? CreateDownloader(),
            verifier ?? FakeVerifier.Trusted,
            runner ?? new FakeRunner(exitCode: 0),
            notices ?? TextWriter.Null,
            policy: new DotnetupPolicy(_policyStore));
        _artifacts.Add(artifact);
        return artifact;
    }

    private sealed class FakeVerifier(InstallerSignatureResult result) : IInstallerSignatureVerifier
    {
        public const string UntrustedReason = "not signed by Microsoft";

        public static FakeVerifier Trusted { get; } = new(InstallerSignatureResult.Trusted);

        public static FakeVerifier Untrusted { get; } = new(InstallerSignatureResult.Untrusted(UntrustedReason));

        public InstallerSignatureResult Verify(string installerPath) => result;
    }

    private sealed class FakeRunner(int exitCode) : IBundleProcessRunner
    {
        public List<(string Path, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public Action<string>? OnRun { get; init; }

        /// <summary>Simulates the user pressing Ctrl+C while the installer runs.</summary>
        public bool RequestCancellation { get; init; }

        public int Run(string bundlePath, IReadOnlyList<string> arguments, Action onCancelRequested)
        {
            Calls.Add((bundlePath, arguments));
            if (RequestCancellation)
            {
                onCancelRequested();
            }

            OnRun?.Invoke(bundlePath);
            return exitCode;
        }
    }
}
