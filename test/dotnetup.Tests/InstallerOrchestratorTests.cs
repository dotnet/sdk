// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InstallerOrchestratorTests
{
    #region InstallAlreadyExists

    [TestMethod]
    public void PrepareInstall_UntrackedAlreadyInstalled_ReturnsAlreadyInstalledResult()
    {
        using var testEnv = new TestEnvironment();
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);
        var resolvedVersion = new ReleaseVersion("10.0.100");
        var request = new DotnetInstallRequest(
            root,
            new UpdateChannel("10.0.100"),
            InstallComponent.SDK,
            new InstallRequestOptions
            {
                Untracked = true,
                ManifestPath = testEnv.ManifestPath
            });
        var resolvedRequest = new ResolvedInstallRequest(request, resolvedVersion);
        var validator = new MockInstallationValidator(validateResult: true);

        using var reporter = new LazyProgressReporter(new NullProgressTarget());
        var batchContext = new InstallBatchContext(reporter, resolvedVersion.ToString().Length);

        var prepared = InstallerOrchestratorSingleton.Instance.PrepareInstall(
            resolvedRequest,
            batchContext,
            out var alreadyInstalledResult,
            validator);

        prepared.Should().BeNull();
        alreadyInstalledResult.Should().NotBeNull();
        alreadyInstalledResult!.WasAlreadyInstalled.Should().BeTrue();
        alreadyInstalledResult.Install.InstallRoot.Path.Should().Be(testEnv.InstallPath);
        alreadyInstalledResult.Install.Component.Should().Be(InstallComponent.SDK);
        alreadyInstalledResult.Install.Version.ToString().Should().Be("10.0.100");
        validator.ValidateCalls.Should().Be(1);
        validator.LastInstall.Should().NotBeNull();
        validator.LastInstall!.InstallRoot.Path.Should().Be(testEnv.InstallPath);
        validator.LastInstall.Component.Should().Be(InstallComponent.SDK);
        validator.LastInstall.Version.ToString().Should().Be("10.0.100");
    }

    [TestMethod]
    public void InstallAlreadyExists_ReturnsTrueForDuplicateInstall()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, testEnv.InstallPath, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(testEnv.InstallPath, "10.0.100", InstallComponent.SDK);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.InstallAlreadyExists(install).Should().BeTrue();
    }

    [TestMethod]
    public void InstallAlreadyExists_ReturnsFalseForDifferentVersion()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, testEnv.InstallPath, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(testEnv.InstallPath, "10.0.101", InstallComponent.SDK);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [TestMethod]
    public void InstallAlreadyExists_ReturnsFalseForDifferentComponent()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, testEnv.InstallPath, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(testEnv.InstallPath, "10.0.100", InstallComponent.Runtime);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [TestMethod]
    public void InstallAlreadyExists_ReturnsFalseForDifferentRoot()
    {
        using var testEnv = new TestEnvironment();
        var altRoot = Path.Combine(testEnv.TempRoot, "other-dotnet");
        Directory.CreateDirectory(altRoot);
        var manifest = CreateManifestWithInstallation(testEnv, testEnv.InstallPath, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(altRoot, "10.0.100", InstallComponent.SDK);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [TestMethod]
    public void InstallAlreadyExists_ReturnsFalseForEmptyManifest()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var install = MakeInstall(testEnv.InstallPath, "10.0.100", InstallComponent.SDK);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [TestMethod]
    public void InstallAlreadyExists_DetectsDuplicateAmongMultipleInstallations()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        // Create component directories so ReadManifest() pruning keeps them.
        testEnv.StubComponentDirectories(null,
            (InstallComponent.SDK, "9.0.100"),
            (InstallComponent.Runtime, "9.0.0"),
            (InstallComponent.SDK, "10.0.100"));

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.AddInstallation(root, new Installation { Component = InstallComponent.SDK, Version = "9.0.100" });
        manifest.AddInstallation(root, new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" });
        manifest.AddInstallation(root, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });

        var install = MakeInstall(testEnv.InstallPath, "10.0.100", InstallComponent.SDK);
        manifest.InstallAlreadyExists(install).Should().BeTrue();
    }

    #endregion

    #region IsRootTracked

    [TestMethod]
    public void IsRootTracked_ReturnsTrueWhenRootExists()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, testEnv.InstallPath, InstallComponent.SDK, "10.0.100");
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.IsRootTracked(root).Should().BeTrue();
    }

    [TestMethod]
    public void IsRootTracked_ReturnsFalseWhenRootDoesNotExist()
    {
        using var testEnv = new TestEnvironment();
        var altRoot = Path.Combine(testEnv.TempRoot, "other-dotnet");
        Directory.CreateDirectory(altRoot);
        var manifest = CreateManifestWithInstallation(testEnv, testEnv.InstallPath, InstallComponent.SDK, "10.0.100");
        var root = new DotnetInstallRoot(altRoot, InstallArchitecture.x64);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.IsRootTracked(root).Should().BeFalse();
    }

    [TestMethod]
    public void IsRootTracked_ReturnsFalseForEmptyManifest()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.IsRootTracked(root).Should().BeFalse();
    }

    #endregion

    #region HasDotnetArtifacts

    [TestMethod]
    public void HasDotnetArtifacts_ReturnsFalseForNullPath()
    {
        DotnetupSharedManifest.HasDotnetArtifacts(null).Should().BeFalse();
    }

    [TestMethod]
    public void HasDotnetArtifacts_ReturnsFalseForNonexistentPath()
    {
        DotnetupSharedManifest.HasDotnetArtifacts(Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N")))
            .Should().BeFalse();
    }

    [TestMethod]
    public void HasDotnetArtifacts_ReturnsTrueWhenSdkDirectoryExists()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk"));

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [TestMethod]
    public void HasDotnetArtifacts_ReturnsTrueWhenSharedDirectoryExists()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "shared"));

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [TestMethod]
    public void HasDotnetArtifacts_ReturnsTrueWhenDotnetExeExists()
    {
        using var testEnv = new TestEnvironment();
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        File.WriteAllText(Path.Combine(testEnv.InstallPath, exeName), "fake");

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [TestMethod]
    public void HasDotnetArtifacts_ReturnsFalseForEmptyDirectory()
    {
        using var testEnv = new TestEnvironment();

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeFalse();
    }

    #endregion

    #region Install mechanism

    [TestMethod]
    public void CreateInstallArtifact_UsesArchiveExtractorForArchiveRequests()
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());

        using var artifact = InstallerOrchestratorSingleton.CreateInstallArtifact(
            MakeResolvedRequest(testEnv, InstallMechanism.Archive, untracked: false),
            new ReleaseManifest(),
            new InstallBatchContext(reporter, 8));

        artifact.Should().BeOfType<DotnetArchiveExtractor>();
    }

    [TestMethod]
    public void CreateInstallArtifact_UsesWindowsBundleArtifactForBundleRequests()
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());

        using var artifact = InstallerOrchestratorSingleton.CreateInstallArtifact(
            MakeResolvedRequest(testEnv, InstallMechanism.WindowsInstallerBundle, untracked: true),
            new ReleaseManifest(),
            new InstallBatchContext(reporter, 8));

        artifact.Should().BeOfType<WindowsBundleArtifact>();
    }

    [TestMethod]
    [DataRow(InstallMechanism.WindowsInstallerBundle, false)]
    [DataRow(InstallMechanism.WindowsInstallerBundle, true)]
    [DataRow(InstallMechanism.Archive, false)]
    internal void CreateInstallArtifact_OnlyArchivesUseOverriddenCache(InstallMechanism mechanism, bool directBundle)
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());
        DotnetupPaths.SetTestDataDirectoryOverride(testEnv.TempRoot);
        try
        {
            var request = MakeResolvedRequest(testEnv, mechanism, untracked: true);
            var batch = new InstallBatchContext(reporter, 8);
            using IInstallArtifact artifact = directBundle
                ? new WindowsBundleArtifact(request.Request, request.ResolvedVersion, batch)
                : InstallerOrchestratorSingleton.CreateInstallArtifact(request, new ReleaseManifest(), batch);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string fieldName = mechanism == InstallMechanism.WindowsInstallerBundle ? "_downloader" : "_archiveDownloader";
            var downloader = (DotnetDownloader)artifact.GetType().GetField(fieldName, flags)!.GetValue(artifact)!;
            var cache = (DownloadCache)typeof(DotnetDownloader).GetField("_downloadCache", flags)!.GetValue(downloader)!;
            var cacheDirectory = (string)typeof(DownloadCache).GetField("_cacheDirectory", flags)!.GetValue(cache)!;
            string expected = mechanism == InstallMechanism.WindowsInstallerBundle
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                    "dotnetup", "downloadcache")
                : DotnetupPaths.DownloadCacheDirectory;

            cacheDirectory.Should().Be(expected);
        }
        finally
        {
            DotnetupPaths.ClearTestDataDirectoryOverride();
        }
    }

    [TestMethod]
    public void CreateInstallationValidator_SelectsValidatorByMechanism()
    {
        InstallerOrchestratorSingleton.CreateInstallationValidator(InstallMechanism.Archive)
            .Should().BeOfType<ArchiveInstallationValidator>();
        InstallerOrchestratorSingleton.CreateInstallationValidator(InstallMechanism.WindowsInstallerBundle)
            .Should().BeOfType<HostFxrInstallationValidator>();
    }

    [TestMethod]
    public void PrepareInstall_TrackedBundleRequest_ThrowsAdminModeRequiresUntracked()
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());
        var validator = new MockInstallationValidator(validateResult: true);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.PrepareInstall(
            MakeResolvedRequest(testEnv, InstallMechanism.WindowsInstallerBundle, untracked: false),
            new InstallBatchContext(reporter, 8),
            out _,
            validator));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeRequiresUntracked);
        validator.ValidateCalls.Should().Be(0);
        File.Exists(testEnv.ManifestPath).Should().BeFalse();
    }

    [TestMethod]
    public void PrepareInstall_BundleAlreadyInstalled_ReturnsAlreadyInstalledWithoutDownloading()
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());
        var validator = new MockInstallationValidator(validateResult: true);

        var prepared = InstallerOrchestratorSingleton.Instance.PrepareInstall(
            MakeResolvedRequest(testEnv, InstallMechanism.WindowsInstallerBundle, untracked: true),
            new InstallBatchContext(reporter, 8),
            out var alreadyInstalledResult,
            validator);

        prepared.Should().BeNull();
        alreadyInstalledResult!.WasAlreadyInstalled.Should().BeTrue();
        validator.ValidateCalls.Should().Be(1);
        File.Exists(testEnv.ManifestPath).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public void CommitPreparedInstall_RechecksBundleBeforeRunningInstaller(bool alreadyInstalled)
    {
        using var testEnv = new TestEnvironment();
        string dotnetHost = SelfUpdateTestFiles.DotnetHostPath;
        string dotnetDirectory = Path.GetDirectoryName(dotnetHost)!;
        string sourceFxr = Directory.EnumerateFiles(Path.Combine(dotnetDirectory, "host", "fxr"), "hostfxr.dll", SearchOption.AllDirectories).First();
        string destinationFxr = Path.Combine(testEnv.InstallPath, "host", "fxr", Path.GetFileName(Path.GetDirectoryName(sourceFxr)!));
        Directory.CreateDirectory(destinationFxr);
        File.Copy(sourceFxr, Path.Combine(destinationFxr, "hostfxr.dll"));
        File.Copy(dotnetHost, Path.Combine(testEnv.InstallPath, "dotnet.exe"));

        var resolvedRequest = MakeResolvedRequest(testEnv, InstallMechanism.WindowsInstallerBundle, untracked: true);
        var install = new DotnetInstall(resolvedRequest.Request.InstallRoot, resolvedRequest.ResolvedVersion, InstallComponent.SDK);
        void InstallSdk()
        {
            string sdkDirectory = Path.Combine(testEnv.InstallPath, "sdk", install.Version.ToString());
            Directory.CreateDirectory(sdkDirectory);
            File.WriteAllText(Path.Combine(sdkDirectory, "dotnet.dll"), string.Empty);
        }

        if (alreadyInstalled)
        {
            InstallSdk();
        }

        var artifact = new FakeArtifact { OnCommit = InstallSdk };
        using var prepared = new InstallerOrchestratorSingleton.PreparedInstall(resolvedRequest, install, artifact);

        var result = InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(prepared);

        result.WasAlreadyInstalled.Should().Be(alreadyInstalled);
        artifact.CommitCalls.Should().Be(alreadyInstalled ? 0 : 1);
        new HostFxrInstallationValidator().Validate(install).Should().BeTrue();
        File.Exists(testEnv.ManifestPath).Should().BeFalse();
    }

    [TestMethod]
    public void CommitPreparedInstall_BundleNotVisibleToHostFxr_ThrowsPostInstallVerificationFailed()
    {
        using var testEnv = new TestEnvironment();
        var resolvedRequest = MakeResolvedRequest(testEnv, InstallMechanism.WindowsInstallerBundle, untracked: true);
        var install = new DotnetInstall(resolvedRequest.Request.InstallRoot, resolvedRequest.ResolvedVersion, InstallComponent.SDK);
        var artifact = new FakeArtifact();
        using var prepared = new InstallerOrchestratorSingleton.PreparedInstall(resolvedRequest, install, artifact);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(prepared));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallerPostInstallVerificationFailed);
        artifact.CommitCalls.Should().Be(1);
        File.Exists(testEnv.ManifestPath).Should().BeFalse();
    }

    [TestMethod]
    public void CommitPreparedInstall_ArchiveValidationFailure_StillThrowsInstallFailed()
    {
        using var testEnv = new TestEnvironment();
        var resolvedRequest = MakeResolvedRequest(testEnv, InstallMechanism.Archive, untracked: true);
        var install = new DotnetInstall(resolvedRequest.Request.InstallRoot, resolvedRequest.ResolvedVersion, InstallComponent.SDK);
        using var prepared = new InstallerOrchestratorSingleton.PreparedInstall(resolvedRequest, install, new FakeArtifact());

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(prepared));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallFailed);
    }

    [TestMethod]
    public void ConsumePreparedInstalls_WithoutCancellation_CommitsEveryInstall()
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());
        var batchContext = new InstallBatchContext(reporter, 8);
        var artifacts = new[] { new FakeArtifact(), new FakeArtifact() };
        var (results, failures) = ConsumePrepared(testEnv, batchContext, artifacts);

        results.Should().HaveCount(2);
        failures.Should().BeEmpty();
        artifacts.Should().OnlyContain(a => a.CommitCalls == 1 && a.Disposed);
    }

    [TestMethod]
    public void ConsumePreparedInstalls_CancellationRequestedDuringCommit_RecordsRemainingInstallsAsCancelled()
    {
        using var testEnv = new TestEnvironment();
        using var reporter = new LazyProgressReporter(new NullProgressTarget());
        var batchContext = new InstallBatchContext(reporter, 8);
        var first = new FakeArtifact { OnCommit = batchContext.RequestCancellation };
        var second = new FakeArtifact();
        var (results, failures) = ConsumePrepared(testEnv, batchContext, first, second);

        results.Should().ContainSingle();
        failures.Should().ContainSingle().Which.Exception.ErrorCode.Should().Be(DotnetInstallErrorCode.OperationCancelled);
        first.CommitCalls.Should().Be(1);
        second.CommitCalls.Should().Be(0);
        second.Disposed.Should().BeTrue();
    }

    private static (List<InstallResult> Results, List<InstallFailure> Failures) ConsumePrepared(
        TestEnvironment testEnv,
        InstallBatchContext batchContext,
        params FakeArtifact[] artifacts)
    {
        var prepared = artifacts.Select(artifact =>
        {
            var resolvedRequest = MakeResolvedRequest(testEnv, InstallMechanism.Archive, untracked: true);
            var install = new DotnetInstall(resolvedRequest.Request.InstallRoot, resolvedRequest.ResolvedVersion, InstallComponent.SDK);
            return new InstallerOrchestratorSingleton.PreparedInstall(resolvedRequest, install, artifact);
        });
        using var readyQueue = new System.Collections.Concurrent.BlockingCollection<InstallerOrchestratorSingleton.PreparedInstall>(
            new System.Collections.Concurrent.ConcurrentQueue<InstallerOrchestratorSingleton.PreparedInstall>(prepared));
        readyQueue.CompleteAdding();
        var results = new List<InstallResult>();
        var failures = new List<InstallFailure>();
        var fatalExceptions = new List<Exception>();

        InstallerOrchestratorSingleton.ConsumeConcurrentPreparedInstallsAndCommit(
            readyQueue,
            batchContext,
            prepared =>
            {
                prepared.Artifact.Commit();
                return new InstallResult(prepared.Install, WasAlreadyInstalled: false);
            },
            results,
            failures,
            fatalExceptions,
            new object());

        fatalExceptions.Should().BeEmpty();
        return (results, failures);
    }

    private static ResolvedInstallRequest MakeResolvedRequest(TestEnvironment testEnv, InstallMechanism mechanism, bool untracked)
    {
        var request = new DotnetInstallRequest(
            new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64),
            new UpdateChannel("10.0.100"),
            InstallComponent.SDK,
            new InstallRequestOptions
            {
                Untracked = untracked,
                ManifestPath = testEnv.ManifestPath,
                InstallMechanism = mechanism,
            });
        return new ResolvedInstallRequest(request, new ReleaseVersion("10.0.100"));
    }

    private sealed class FakeArtifact : IInstallArtifact
    {
        public int CommitCalls { get; private set; }

        public bool Disposed { get; private set; }

        public Action? OnCommit { get; init; }

        public IReadOnlyList<string> ExtractedSubcomponents => [];

        public void Prepare()
        {
        }

        public void Commit()
        {
            CommitCalls++;
            OnCommit?.Invoke();
        }

        public void Dispose() => Disposed = true;
    }

    #endregion

    #region Helpers

    private static DotnetInstall MakeInstall(string path, string version, InstallComponent component)
    {
        return new DotnetInstall(
            new DotnetInstallRoot(path, InstallArchitecture.x64),
            new ReleaseVersion(version),
            component);
    }

    private static DotnetupSharedManifest CreateManifestWithInstallation(TestEnvironment testEnv, string rootPath, InstallComponent component, string version)
    {
        testEnv.StubComponentDirectories(rootPath, (component, version));

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(rootPath, InstallArchitecture.x64);
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.AddInstallation(root, new Installation { Component = component, Version = version });
        return manifest;
    }

    #endregion
}
