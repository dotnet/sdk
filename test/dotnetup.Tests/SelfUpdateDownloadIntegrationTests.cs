// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateDownloadIntegrationTests : SdkTest
{
    [TestInitialize]
    public void AllowUnsignedSource() => UnsignedSourcePolicy.OverrideForTesting = () => false;

    [TestCleanup]
    public void ResetUnsignedSourcePolicy() => UnsignedSourcePolicy.OverrideForTesting = null;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NativeWorkflowDownloadsVerifiedReleaseAndRunsDefaultChildVerification()
    {
        using var files = new NativeSelfUpdateFiles();
        using var handler = new NativeSelfUpdateDownloadHandler(files);
        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(files, http);
        byte[] originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);

        Assert.AreEqual(files.Release.Version.ToString(), SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(CreateWorkflow(files, downloader)));

        AssertPinnedRequests(handler);
        AssertInstalledReplacement(files);
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath + ".exe"));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath + ".download"));
        string[] backups = Directory.GetFiles(files.Paths.DirectoryPath, "dotnetup.exe.old.*");
        Assert.HasCount(1, backups);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(backups[0]));
        AssertLocksAvailable(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NativeWorkflowRejectsHashMismatchWithoutReplacingOrCaching()
    {
        using var files = new NativeSelfUpdateFiles();
        using var handler = new NativeSelfUpdateDownloadHandler(files) { PublishedHash = new string('0', 128) };
        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(files, http);
        byte[] originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(CreateWorkflow(files, downloader)));

        Assert.AreEqual(DotnetInstallErrorCode.HashMismatch, exception.ErrorCode);
        AssertPinnedRequests(handler);
        AssertOriginalUnchanged(files, originalBytes);
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath + ".download"));
        Assert.IsNull(new DownloadCache(Path.Combine(files.Paths.DirectoryPath, "cache")).GetCachedFilePath(handler.ArtifactUri.AbsoluteUri));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NativeWorkflowRestoresOriginalOnVersionMismatchDespiteValidHash()
    {
        using var files = new NativeSelfUpdateFiles();
        const string advertisedVersion = "0.2.0-preview.1.99999.3";
        using var handler = new NativeSelfUpdateDownloadHandler(files, advertisedVersion);
        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(files, http);
        byte[] originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        Assert.AreNotEqual(files.OriginalVersion, advertisedVersion);
        Assert.AreNotEqual(files.ReplacementVersion, advertisedVersion);

        var workflow = new SelfUpdateWorkflow(files.Paths, files.OriginalVersion,
            () => downloader.ResolveDotnetupDownload(files.Release.Rid),
            (release, destination) => downloader.DownloadWithVerification(release, destination));
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(workflow));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
        AssertPinnedRequests(handler);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        var rejected = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*.rejected");
        Assert.HasCount(1, rejected);
        Assert.AreSequenceEqual(File.ReadAllBytes(files.ReplacementPath), File.ReadAllBytes(rejected[0]));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath + ".download"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NativeWorkflowKeepsConcreteReleasePinnedWhenDailyMovesAfterResolution()
    {
        using var files = new NativeSelfUpdateFiles();
        using var handler = new NativeSelfUpdateDownloadHandler(files);
        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(files, http);
        var movedDailyUri = new Uri("https://ci.dot.net/public/dotnetup/0.2.0-preview.1.99999.3/dotnetup-win-x64.exe");
        Assert.AreNotEqual(handler.ArtifactUri, movedDailyUri);
        var workflow = CreateWorkflow(files, downloader, () =>
        {
            Assert.HasCount(2, handler.Requests);
            handler.DailyFinalUri = movedDailyUri;
        });

        Assert.AreEqual(files.Release.Version.ToString(), SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(workflow));

        Assert.AreEqual(movedDailyUri, handler.DailyFinalUri);
        AssertPinnedRequests(handler);
        AssertInstalledReplacement(files);
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        AssertLocksAvailable(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NativeWorkflowRechecksUnsignedPolicyAfterResolutionEvenWithCachedBinary()
    {
        using var files = new NativeSelfUpdateFiles();
        using var handler = new NativeSelfUpdateDownloadHandler(files);
        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(files, http);
        var cache = new DownloadCache(Path.Combine(files.Paths.DirectoryPath, "cache"));
        cache.AddToCache(handler.ArtifactUri.AbsoluteUri, files.ReplacementPath);
        Assert.IsNotNull(cache.GetCachedFilePath(handler.ArtifactUri.AbsoluteUri));
        byte[] originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var workflow = CreateWorkflow(files, downloader, () => UnsignedSourcePolicy.OverrideForTesting = () => true);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(workflow));

        Assert.AreEqual(DotnetInstallErrorCode.UnsignedDownloadBlockedByPolicy, exception.ErrorCode);
        Assert.AreSequenceEqual(new[] { NativeSelfUpdateDownloadHandler.DailyUri, handler.ChecksumUri }, handler.Requests);
        AssertOriginalUnchanged(files, originalBytes);
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath + ".download"));
    }

    private static DotnetDownloader CreateDownloader(NativeSelfUpdateFiles files, HttpClient http)
        => new(new ReleaseManifest(), http, Path.Combine(files.Paths.DirectoryPath, "cache"));

    private static SelfUpdateWorkflow CreateWorkflow(NativeSelfUpdateFiles files, DotnetDownloader downloader, Action? afterResolution = null)
        => new(files.Paths, files.OriginalVersion, () =>
        {
            var release = downloader.ResolveDotnetupDownload(files.Release.Rid);
            Assert.AreEqual(files.Release.Version, release.Version);
            Assert.IsTrue(release.IsUnsigned);
            afterResolution?.Invoke();
            return release;
        }, (release, destination) =>
        {
            Assert.AreEqual(files.Paths.StagedPath, destination);
            Assert.AreEqual(destination, downloader.DownloadWithVerification(release, destination));
        });

    private static void AssertPinnedRequests(NativeSelfUpdateDownloadHandler handler)
        => Assert.AreSequenceEqual(new[] { NativeSelfUpdateDownloadHandler.DailyUri, handler.ChecksumUri, handler.ArtifactUri }, handler.Requests);

    private static void AssertInstalledReplacement(NativeSelfUpdateFiles files)
    {
        Assert.AreSequenceEqual(File.ReadAllBytes(files.ReplacementPath), File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreEqual(files.ReplacementVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
        Assert.StartsWith(files.Release.Version.ToString(), files.Run(["--version"]));
        Assert.Contains(files.Release.Version.ToString(), files.Run(["--info"]));
    }

    private static void AssertOriginalUnchanged(NativeSelfUpdateFiles files, byte[] originalBytes)
    {
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreEqual(files.OriginalIdentity, SelfUpdatePaths.ReadVersionMetadata(files.Paths.InstalledPath));
        Assert.StartsWith(files.OriginalIdentity.Split('|')[0], files.Run(["--version"]));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "dotnetup.exe.old.*"));
        AssertLocksAvailable(files.Paths);
    }

    private static void AssertLocksAvailable(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
        Assert.IsNotNull(update);
        Assert.IsNotNull(activity);
    }
}