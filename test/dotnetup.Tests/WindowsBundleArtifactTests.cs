// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
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

    public WindowsBundleArtifactTests()
    {
        UnsignedSourcePolicy.OverrideForTesting = null;
    }

    public void Dispose()
    {
        UnsignedSourcePolicy.OverrideForTesting = null;
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
    public void Prepare_NonExeDownload_Throws()
    {
        var downloader = CreateDownloader();
        downloader.ArchiveFileExtension = ".zip";
        var artifact = CreateArtifact(StableChannel, downloader: downloader);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Prepare);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallerFailed);
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
        UnsignedSourcePolicy.OverrideForTesting = () => false;
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(StableChannel, verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
        ex.Message.Should().Contain(FakeVerifier.UntrustedReason);
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("daily")]
    [DataRow("10.0.100-preview.4.25216.37")]
    public void Commit_UnsignedSourceAllowedByPolicy_RunsInstaller(string channel)
    {
        UnsignedSourcePolicy.OverrideForTesting = () => false;
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(channel, verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        artifact.Commit();

        runner.Calls.Should().ContainSingle();
    }

    [TestMethod]
    public void Commit_UnsignedSourceBlockedByPolicy_ThrowsAndDoesNotRunInstaller()
    {
        UnsignedSourcePolicy.OverrideForTesting = () => true;
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact("daily", verifier: FakeVerifier.Untrusted, runner: runner);
        artifact.Prepare();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void Commit_BundleModifiedAfterDownload_ThrowsAndDoesNotRunInstaller()
    {
        var runner = new FakeRunner(exitCode: 0);
        var artifact = CreateArtifact(StableChannel, runner: runner);
        artifact.Prepare();
        File.WriteAllBytes(artifact.BundlePath!, [9, 9, 9]);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(artifact.Commit);

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.HashMismatch);
        runner.Calls.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(3010, true)]
    [DataRow(1641, true)]
    public void Commit_SuccessExitCodes_Succeed(int exitCode, bool restartRequired)
    {
        using var notices = new StringWriter(CultureInfo.InvariantCulture);
        var artifact = CreateArtifact(StableChannel, runner: new FakeRunner(exitCode), notices: notices);
        artifact.Prepare();

        artifact.Commit();

        if (restartRequired)
        {
            notices.ToString().Should().Contain("restart is required");
        }
        else
        {
            notices.ToString().Should().BeEmpty();
        }
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
    [DataRow(1641, BundleExitOutcome.SuccessRestartRequired)]
    [DataRow(1602, BundleExitOutcome.Cancelled)]
    [DataRow(1223, BundleExitOutcome.Cancelled)]
    [DataRow(1618, BundleExitOutcome.Busy)]
    [DataRow(740, BundleExitOutcome.ElevationRequired)]
    [DataRow(1, BundleExitOutcome.Failed)]
    internal void BundleExitCodes_Classify(int exitCode, BundleExitOutcome expected)
    {
        BundleExitCodes.Classify(exitCode).Should().Be(expected);
    }

    private static MockArchiveDownloader CreateDownloader() => new()
    {
        ArchiveFileExtension = ".exe",
        FakeArchiveContent = [0x4D, 0x5A, 1, 2, 3, 4],
    };

    private WindowsBundleArtifact CreateArtifact(
        string channel,
        MockArchiveDownloader? downloader = null,
        IInstallerSignatureVerifier? verifier = null,
        FakeRunner? runner = null,
        TextWriter? notices = null)
    {
        var request = new DotnetInstallRequest(
            new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-bundle-root-unused"), InstallArchitecture.x64),
            new UpdateChannel(channel),
            InstallComponent.SDK,
            new InstallRequestOptions { Untracked = true, InstallMechanism = InstallMechanism.WindowsInstallerBundle });
        var artifact = new WindowsBundleArtifact(
            request,
            new ReleaseVersion("10.0.100"),
            new InstallBatchContext(_reporter, 8),
            downloader ?? CreateDownloader(),
            verifier ?? FakeVerifier.Trusted,
            runner ?? new FakeRunner(exitCode: 0),
            notices ?? TextWriter.Null);
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

        public int Run(string bundlePath, IReadOnlyList<string> arguments)
        {
            Calls.Add((bundlePath, arguments));
            OnRun?.Invoke(bundlePath);
            return exitCode;
        }
    }
}
