// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Tar;
using System.IO;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for DotnetArchiveExtractor, particularly error handling scenarios.
/// </summary>
[TestClass]
public class DotnetArchiveExtractorTests
{
    private readonly ITestOutputHelper _log;

    public DotnetArchiveExtractorTests(TestContext testContext)
    {
        _log = new TestContextOutputHelper(testContext);
    }

    [TestMethod]
    [DataRow("11.0.100-rtm.26552.115", "11.0.100", InstallComponent.SDK, "sdk/11.0.100/dotnet.dll")]
    [DataRow("10.0.115-servicing.26552.102", "10.0.115", InstallComponent.SDK, "sdk/10.0.115/dotnet.dll")]
    [DataRow("11.0.0-rtm.26552.115", "11.0.0", InstallComponent.Runtime, "shared/Microsoft.NETCore.App/11.0.0/Microsoft.NETCore.App.deps.json")]
    public void Commit_DailyReleasePayload_ValidatesAndTracksBothVersions(
        string buildVersion, string productVersion, InstallComponent component, string markerPath)
    {
        using var testEnv = new TestEnvironment();
        var resolvedVersion = new ReleaseVersion(buildVersion);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        using var prepared = PrepareArchiveInstall(testEnv, component, "daily", buildVersion, productVersion, markerPath, "component marker");
        var result = InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(prepared);

        result.Install.Version.Should().Be(resolvedVersion);
        new InstallationValidator().Validate(result.Install).Should().BeTrue();
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var installation = manifest.ReadManifest().DotnetRoots.Single().Installations.Single();
            installation.Version.Should().Be(buildVersion);
            installation.ProductVersion.Should().Be(productVersion);
            manifest.InstallAlreadyExists(new DotnetInstall(root, resolvedVersion, component)).Should().BeTrue();
        }

        var listed = InstallationLister.GetListData(verify: true, manifestPath: testEnv.ManifestPath).Installations.Single();
        listed.Version.Should().Be(buildVersion);
        listed.IsValid.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("11-daily", "11.0.100-rtm.26552.115", "11.0.100", "11-daily", "11.0.100-rtm.26553.101", "11.0.100", "sdk uninstall 11-daily")]
    [DataRow("11-daily", "11.0.100-rtm.26552.115", "11.0.100", "11.0.100", "11.0.100", null, "sdk uninstall 11-daily")]
    [DataRow("11.0.100", "11.0.100", null, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", "sdk uninstall 11.0.100")]
    public void Commit_DifferentBuildWithSameLayout_RejectsWithoutChangingExistingInstall(
        string existingChannel, string existingVersion, string? existingPayload,
        string newChannel, string newVersion, string? newPayload, string expectedUninstall)
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var existing = PrepareArchiveInstall(testEnv, InstallComponent.SDK, existingChannel, existingVersion, existingPayload, markerPath, "existing"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(existing);
        }
        using var replacement = PrepareArchiveInstall(testEnv, InstallComponent.SDK, newChannel, newVersion, newPayload, markerPath, "replacement");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(replacement));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
        ex.Message.Should().Contain($".NET SDK {newVersion} replaces .NET SDK {existingVersion}")
            .And.Contain($"dotnetup {expectedUninstall} --install-path");
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("existing");
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        new DotnetupSharedManifest(testEnv.ManifestPath).ReadManifest().DotnetRoots.Single().Installations
            .Should().ContainSingle().Which.Version.Should().Be(existingVersion);
    }

    [TestMethod]
    public void Commit_TrackedReleaseOverDailyBuildFromSameCommit_RecordsBothInstallations()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "same-build"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }
        using var release = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, "same-build");

        InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(release).WasAlreadyInstalled.Should().BeFalse();

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        new DotnetupSharedManifest(testEnv.ManifestPath).ReadManifest().DotnetRoots.Single().Installations
            .Should().HaveCount(2).And.OnlyContain(i => i.LayoutVersion == "11.0.100" && i.Subcomponents.Contains("sdk/11.0.100"));
    }

    [TestMethod]
    public void Commit_LayoutConflict_SuggestsOnlySpecsThatKeepTheConflictingInstall()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var pinned = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, "existing"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(pinned);
        }
        using (var band = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.1xx", "11.0.105", null, "sdk/11.0.105/dotnet.dll", "newer"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(band);
        }
        using var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "daily");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily));

        ex.Message.Should().Contain("sdk uninstall 11.0.100 ").And.NotContain("11.0.1xx");
    }

    [TestMethod]
    public void Commit_LayoutConflict_SuggestsSpecsOfEveryInstallSharingTheFolder()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "same-build"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }
        using (var release = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, "same-build"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(release);
        }
        using var nextDaily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.1xx-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "next-build");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(nextDaily));

        ex.Message.Should().Contain("sdk uninstall 11-daily ").And.Contain("sdk uninstall 11.0.100 ");
    }

    [TestMethod]
    public void Commit_LayoutConflictKeptByNoSpec_SuggestsRemovingFolderWithoutRemovingAnything()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "old-build"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var data = manifest.ReadManifest();
            data.DotnetRoots.Single().InstallSpecs.Clear();
            manifest.WriteManifest(data);
        }
        string folder = Path.Combine(testEnv.InstallPath, "sdk", "11.0.100");
        using var nextDaily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "new-build");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(nextDaily));

        ex.Message.Should().Contain($"Remove '{folder}'");
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("old-build");

        Directory.Delete(folder, recursive: true);
        InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(nextDaily).WasAlreadyInstalled.Should().BeFalse();
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("new-build");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Commit_GlobalJsonPinSatisfiedByBuildInstalledDuringDownload_ReturnsExistingInstall(bool untracked)
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        var globalJson = Path.Combine(testEnv.TempRoot, "global.json");
        File.WriteAllText(globalJson, """{"sdk":{"version":"11.0.100","rollForward":"disable"}}""");
        using var pinned = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, "release",
            untracked, globalJsonPath: globalJson);
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "daily", untracked))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }

        InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(pinned).WasAlreadyInstalled.Should().BeTrue();

        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("daily");
        if (!untracked)
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            new DotnetupSharedManifest(testEnv.ManifestPath).ReadManifest().DotnetRoots.Single().InstallSpecs
                .Should().Contain(spec => spec.InstallSource == InstallSource.GlobalJson);
        }
    }

    [TestMethod]
    public void Commit_UntrackedDailyBuildWithExistingLayout_Rejects()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var existing = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "existing", untracked: true))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(existing);
        }
        using var replacement = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "replacement", untracked: true);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(replacement));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
        ex.Message.Should().Contain("Remove '");
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("existing");
    }

    [TestMethod]
    public void Commit_TrackedRootDailyBuildWithUntrackedLayout_Rejects()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var existing = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, "existing", untracked: true))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(existing);
        }
        using (var tracked = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "10.0.100", "10.0.100", null, "sdk/10.0.100/dotnet.dll", "tracked"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(tracked);
        }
        using var replacement = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "replacement");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(replacement));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
        ex.Message.Should().Contain("Remove '");
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("existing");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Commit_LayoutConflictWithGlobalJsonSpec_SuggestsUpdatingRepository(bool includeStandaloneSpec)
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        var globalJson = Path.Combine(testEnv.TempRoot, "global.json");
        File.WriteAllText(globalJson, """{"sdk":{"version":"11.0.100"}}""");
        using (var existing = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, "existing", globalJsonPath: globalJson))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(existing);
        }
        if (includeStandaloneSpec)
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var data = manifest.ReadManifest();
            data.DotnetRoots.Single().InstallSpecs.Add(new InstallSpec
            {
                Component = InstallComponent.SDK, VersionOrChannel = "11.0.100", InstallSource = InstallSource.Migration
            });
            manifest.WriteManifest(data);
        }
        using var replacement = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "replacement");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(replacement));

        ex.Message.Should().Contain("update or delete its global.json").And.Contain(globalJson).And.NotContain("--source");
        if (includeStandaloneSpec)
        {
            ex.Message.Should().Contain("sdk uninstall 11.0.100 ").And.Contain($"--manifest-path \"{testEnv.ManifestPath}\"");
        }
        else
        {
            ex.Message.Should().NotContain("sdk uninstall");
        }
    }

    [TestMethod]
    [DataRow("daily-build", false)]
    [DataRow("release-build", true)]
    public void Commit_UntrackedReleaseOverDailyBuild_ChecksBuildCommit(string releaseCommit, bool expectConflict)
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "daily-build", untracked: true))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }
        using var release = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11.0.100", "11.0.100", null, markerPath, releaseCommit, untracked: true);

        if (expectConflict)
        {
            var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(release));
            ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
            ex.Message.Should().Contain("holds a different build");
        }
        else
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(release).WasAlreadyInstalled.Should().BeTrue();
        }

        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("daily-build");
    }

    [TestMethod]
    [DataRow("runtime-build", false)]
    [DataRow("other-runtime-build", true)]
    public void Commit_SdkBundlingExistingRuntimeFolder_ChecksBuildCommit(string bundledRuntimeCommit, bool expectConflict)
    {
        using var testEnv = new TestEnvironment();
        const string runtimeFolder = "shared/Microsoft.NETCore.App/11.0.0";
        using (var runtime = PrepareArchiveInstall(testEnv, InstallComponent.Runtime, "11.0-daily", "11.0.0-rtm.26552.115", "11.0.0",
            $"{runtimeFolder}/Microsoft.NETCore.App.deps.json", "runtime-build"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(runtime);
        }
        using var sdk = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100",
            "sdk/11.0.100/dotnet.dll", "sdk-build", extraFiles: ($"{runtimeFolder}/.version", $"{bundledRuntimeCommit}\n11.0.0"));

        if (expectConflict)
        {
            var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(sdk));
            ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
            ex.Message.Should().Contain("Microsoft.NETCore.App");
            Directory.Exists(Path.Combine(testEnv.InstallPath, "sdk", "11.0.100")).Should().BeFalse();
        }
        else
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(sdk).WasAlreadyInstalled.Should().BeFalse();
        }
    }

    [TestMethod]
    public void Commit_ExistingLayoutWithoutVersionFile_Rejects()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "11.0.100"));
        File.WriteAllText(Path.Combine(testEnv.InstallPath, markerPath), "existing");
        using var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26553.101", "11.0.100", markerPath, "daily", untracked: true);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
        ex.Message.Should().Contain("has no .version file");
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("existing");
    }

    [TestMethod]
    public void Commit_SdkFromSameCommitWithDifferentBuildVersion_Rejects()
    {
        using var testEnv = new TestEnvironment();
        const string markerPath = "sdk/11.0.100/dotnet.dll";
        using (var first = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", markerPath, "first",
            untracked: true, versionFile: "commit\n11.0.100\nwin-x64\n11.0.100-rtm.26552.115\n11.0.100"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(first);
        }
        using var rebuild = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.116", "11.0.100", markerPath, "rebuild",
            untracked: true, versionFile: "commit\n11.0.100\nwin-x64\n11.0.100-rtm.26552.116\n11.0.100");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(rebuild));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.InstallLayoutConflict);
        File.ReadAllText(Path.Combine(testEnv.InstallPath, markerPath)).Should().Be("first");
    }

    [TestMethod]
    public void PrepareInstall_TrackedDailyBuildAlreadyInstalled_ReturnsOnDiskVersion()
    {
        using var testEnv = new TestEnvironment();
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100", "sdk/11.0.100/dotnet.dll", "daily"))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var request = new DotnetInstallRequest(root, new UpdateChannel("11-daily"), InstallComponent.SDK,
            new InstallRequestOptions { ManifestPath = testEnv.ManifestPath });
        using var reporter = new LazyProgressReporter(new NullProgressTarget());

        var prepared = InstallerOrchestratorSingleton.Instance.PrepareInstall(
            new ResolvedInstallRequest(request, new ReleaseVersion("11.0.100-rtm.26552.115")), new InstallBatchContext(reporter, 0), out var result);

        prepared.Should().BeNull();
        result!.WasAlreadyInstalled.Should().BeTrue();
        result.Install.LayoutVersion.ToString().Should().Be("11.0.100");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PrepareInstall_GlobalJsonPinSatisfiedByDailyBuildFolder_DoesNotDownload(bool untracked)
    {
        using var testEnv = new TestEnvironment();
        using (var daily = PrepareArchiveInstall(testEnv, InstallComponent.SDK, "11-daily", "11.0.100-rtm.26552.115", "11.0.100",
            "sdk/11.0.100/dotnet.dll", "daily", untracked))
        {
            InstallerOrchestratorSingleton.Instance.CommitPreparedInstall(daily);
        }
        var globalJson = Path.Combine(testEnv.TempRoot, "global.json");
        File.WriteAllText(globalJson, """{"sdk":{"version":"11.0.100","rollForward":"disable"}}""");
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var request = new DotnetInstallRequest(root, new UpdateChannel("11.0.100"), InstallComponent.SDK,
            new InstallRequestOptions
            {
                ManifestPath = testEnv.ManifestPath, Untracked = untracked,
                GlobalJsonPath = globalJson, InstallSource = InstallSource.GlobalJson,
            });
        using var reporter = new LazyProgressReporter(new NullProgressTarget());

        var prepared = InstallerOrchestratorSingleton.Instance.PrepareInstall(
            new ResolvedInstallRequest(request, new ReleaseVersion("11.0.100")), new InstallBatchContext(reporter, 0), out var result);

        prepared.Should().BeNull();
        result!.WasAlreadyInstalled.Should().BeTrue();
        result.Install.LayoutVersion.ToString().Should().Be("11.0.100");
    }

    private static InstallerOrchestratorSingleton.PreparedInstall PrepareArchiveInstall(
        TestEnvironment testEnv, InstallComponent component, string channel, string version, string? payloadVersion,
        string markerPath, string markerContent, bool untracked = false, string? globalJsonPath = null, string? versionFile = null,
        params (string path, string content)[] extraFiles)
    {
        var resolvedVersion = new ReleaseVersion(version);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var request = new DotnetInstallRequest(root, new UpdateChannel(channel), component,
            new InstallRequestOptions
            {
                ManifestPath = testEnv.ManifestPath,
                Untracked = untracked,
                GlobalJsonPath = globalJsonPath,
                InstallSource = globalJsonPath is null ? InstallSource.Explicit : InstallSource.GlobalJson,
            });
        var downloader = new MockArchiveDownloader
        {
            ArchiveFileExtension = ".tar.gz",
            FakeArchiveContent = CreateTarGzWithFiles([
                (markerPath, markerContent),
                ($"{markerPath[..markerPath.LastIndexOf('/')]}/.version", versionFile ?? $"{markerContent}\n{payloadVersion ?? version}"),
                (DotnetupUtilities.GetDotnetExeName(), "muxer"),
                .. extraFiles]),
            ProductVersion = payloadVersion is null ? null : new ReleaseVersion(payloadVersion),
            IsUnsigned = true
        };
        var extractor = new DotnetArchiveExtractor(request, resolvedVersion, new ReleaseManifest(), new NullProgressTarget(), downloader);
        extractor.Prepare();
        return new InstallerOrchestratorSingleton.PreparedInstall(new ResolvedInstallRequest(request, resolvedVersion), extractor);
    }

    [TestMethod]
    public void Prepare_DownloadFailure_ThrowsException()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        var mockDownloader = new MockArchiveDownloader
        {
            ExceptionToThrow = new Exception("Network error: Unable to connect")
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        // Act & Assert - Prepare wraps download failures in DotnetInstallException
        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => extractor.Prepare());
        _log.WriteLine($"Exception message: {ex.Message}");
        ex.Message.Should().Contain("Failed to download");
        ex.InnerException!.Message.Should().Contain("Network error");

        // Verify the download was attempted
        mockDownloader.DownloadCalls.Should().HaveCount(1);
        mockDownloader.DownloadCalls[0].Version.Should().Be(version);
    }

    [TestMethod]
    public void ExistingMuxer_IsPreserved_OnExtractionFailure()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Create a fake existing muxer
        var muxerPath = Path.Combine(testEnv.InstallPath, DotnetupUtilities.GetDotnetExeName());
        var originalContent = "existing muxer content";
        File.WriteAllText(muxerPath, originalContent);
        _log.WriteLine($"Created fake muxer at: {muxerPath}");

        // Create a shared runtime directory to simulate existing installation
        var runtimeDir = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "8.0.0");
        Directory.CreateDirectory(runtimeDir);

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        // Create a mock downloader that creates an invalid/empty archive
        var mockDownloader = new MockArchiveDownloader
        {
            CreateFakeArchive = true,
            FakeArchiveContent = new byte[] { 0x00, 0x01, 0x02 } // Invalid archive content
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        // Prepare succeeds (download works)
        extractor.Prepare();
        _log.WriteLine("Prepare completed successfully");

        // Commit should fail due to invalid archive, but muxer should be restored
        var ex = Assert.Throws<Exception>(() => extractor.Commit());
        _log.WriteLine($"Commit failed as expected: {ex.Message}");

        // Verify the muxer was restored
        File.Exists(muxerPath).Should().BeTrue("muxer should be restored after extraction failure");
        File.ReadAllText(muxerPath).Should().Be(originalContent, "muxer content should be preserved");
    }

    [TestMethod]
    [DataRow("corrupt", DotnetInstallErrorCode.ArchiveCorrupted)]
    [DataRow("permission", DotnetInstallErrorCode.PermissionDenied)]
    [DataRow("io", DotnetInstallErrorCode.ExtractionFailed)]
    public void Commit_WhenNativeAndManagedTarFail_ClassifiesManagedFailure(
        string failure, DotnetInstallErrorCode expectedCode)
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var request = new DotnetInstallRequest(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());
        var downloader = new MockArchiveDownloader { ArchiveFileExtension = ".tar" };
        using var extractor = new DotnetArchiveExtractor(
            request, new ReleaseVersion(9, 0, 0), new ReleaseManifest(),
            new NullProgressTarget(), downloader);
        Exception managedFailure = failure switch
        {
            "corrupt" => new InvalidDataException("managed archive failure"),
            "permission" => new UnauthorizedAccessException("managed permission failure"),
            _ => new IOException("managed IO failure"),
        };
        extractor.TarArchiveExtractor = new WindowsNativeTarArchiveExtractor(
            new FailingTarExtractor(managedFailure),
            new FailingNativeTarRunner());

        extractor.Prepare();
        DotnetInstallException exception = Assert.ThrowsExactly<DotnetInstallException>(extractor.Commit);

        exception.ErrorCode.Should().Be(expectedCode);
        var bothFailures = exception.InnerException.Should().BeOfType<AggregateException>().Subject;
        bothFailures.InnerExceptions.Should().HaveCount(2);
        bothFailures.InnerExceptions[0].Message.Should().Contain("native failure");
        bothFailures.InnerExceptions[1].Should().BeSameAs(managedFailure);
    }

    private sealed class FailingTarExtractor(Exception failure) : ITarArchiveExtractor
    {
        public void Extract(TarExtractionContext context) => throw failure;
    }

    private sealed class FailingNativeTarRunner : INativeTarProcessRunner
    {
        public NativeTarProcessResult Run(string executable, IReadOnlyList<string> arguments)
            => new(7, "native failure");
    }

    [TestMethod]
    public void Dispose_CleansUpTemporaryFiles()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        var mockDownloader = new MockArchiveDownloader();
        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        string scratchDir;
        {
            using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

            // Get the scratch directory path via the internal property
            scratchDir = extractor.ScratchDownloadDirectory;
            _log.WriteLine($"Scratch directory: {scratchDir}");

            // Verify it exists during operation
            Directory.Exists(scratchDir).Should().BeTrue("scratch directory should exist during operation");
        }
        // After dispose

        // Verify it was cleaned up
        Directory.Exists(scratchDir).Should().BeFalse("scratch directory should be deleted after Dispose");
        _log.WriteLine("Dispose cleaned up scratch directory successfully");
    }

    [TestMethod]
    public void Prepare_RecordsCorrectDownloadParameters()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 12);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.ASPNETCore,
            new InstallRequestOptions());

        var mockDownloader = new MockArchiveDownloader();
        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        extractor.Prepare();

        // Verify the correct parameters were passed to the downloader
        mockDownloader.DownloadCalls.Should().HaveCount(1);

        var call = mockDownloader.DownloadCalls[0];
        call.Request.Should().Be(request);
        call.Version.Should().Be(version);
        call.DestinationPath.Should().StartWith(extractor.ScratchDownloadDirectory);
        call.DestinationPath.Should().EndWith(DotnetupTestUtilities.DefaultArchiveFileExtension);
        call.HasProgressReporter.Should().BeFalse();

        _log.WriteLine($"Download was called with version {call.Version} to {call.DestinationPath}");
    }

    /// <summary>
    /// Creates a tar file with entries having specified Unix permissions.
    /// </summary>
    private static void CreateTarWithPermissions(string tarPath, params (string name, UnixFileMode mode, bool isDirectory)[] entries)
    {
        using var fs = File.Create(tarPath);
        using var writer = new TarWriter(fs);

        foreach (var (name, mode, isDirectory) in entries)
        {
            TarEntry entry;
            if (isDirectory)
            {
                entry = new PaxTarEntry(TarEntryType.Directory, name) { Mode = mode };
            }
            else
            {
                entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    Mode = mode,
                    DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"content-of-{name}"))
                };
            }

            writer.WriteEntry(entry);
        }
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void ExtractTarContents_PreservesExecutePermission()
    {
        // Arrange — create a tar with an executable (755) and a non-executable (644) entry
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var executableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                           | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                           | UnixFileMode.OtherRead | UnixFileMode.OtherExecute; // 755
        var readOnlyMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                         | UnixFileMode.GroupRead
                         | UnixFileMode.OtherRead; // 644

        CreateTarWithPermissions(tarPath,
            ("bin/dotnet", executableMode, isDirectory: false),
            ("shared/readme.txt", readOnlyMode, isDirectory: false));

        // Act — ExtractToFile should apply Mode via UnixCreateMode
        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, installTask: null);

        // Assert
        var dotnetPath = Path.Combine(extractDir, "bin", "dotnet");
        var readmePath = Path.Combine(extractDir, "shared", "readme.txt");

        File.Exists(dotnetPath).Should().BeTrue();
        File.Exists(readmePath).Should().BeTrue();

#pragma warning disable CA1416 // Validate platform compatibility — test is gated by PlatformSpecificFact
        var dotnetMode = File.GetUnixFileMode(dotnetPath);
        var readmeMode = File.GetUnixFileMode(readmePath);
#pragma warning restore CA1416

        _log.WriteLine($"dotnet mode: {dotnetMode} ({Convert.ToString((int)dotnetMode, 8)})");
        _log.WriteLine($"readme mode: {readmeMode} ({Convert.ToString((int)readmeMode, 8)})");

        dotnetMode.Should().HaveFlag(UnixFileMode.UserExecute, "executable entry should preserve UserExecute");
        readmeMode.Should().NotHaveFlag(UnixFileMode.UserExecute, "non-executable entry should not have UserExecute");
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void ExtractTarContents_PreservesDirectoryPermissions()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var dirMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute; // 755

        CreateTarWithPermissions(tarPath,
            ("mydir/", dirMode, isDirectory: true));

        // Act
        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, installTask: null);

        // Assert
        var dirPath = Path.Combine(extractDir, "mydir");
        Directory.Exists(dirPath).Should().BeTrue();

#pragma warning disable CA1416 // Validate platform compatibility — test is gated by PlatformSpecificFact
        var actualMode = File.GetUnixFileMode(dirPath);
#pragma warning restore CA1416
        _log.WriteLine($"directory mode: {actualMode} ({Convert.ToString((int)actualMode, 8)})");

        actualMode.Should().HaveFlag(UnixFileMode.UserExecute, "directory should preserve UserExecute");
        actualMode.Should().HaveFlag(UnixFileMode.UserRead);
        actualMode.Should().HaveFlag(UnixFileMode.UserWrite);
    }

    [TestMethod]
    public void ExtractTarContents_ExtractsContentCorrectly()
    {
        // Cross-platform test for content correctness
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

        CreateTarWithPermissions(tarPath,
            ("hello.txt", defaultMode, isDirectory: false),
            ("sub/nested.txt", defaultMode, isDirectory: false));

        // Act
        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, installTask: null);

        // Assert
        var helloPath = Path.Combine(extractDir, "hello.txt");
        var nestedPath = Path.Combine(extractDir, "sub", "nested.txt");

        File.Exists(helloPath).Should().BeTrue();
        File.ReadAllText(helloPath).Should().Be("content-of-hello.txt");

        File.Exists(nestedPath).Should().BeTrue();
        File.ReadAllText(nestedPath).Should().Be("content-of-sub/nested.txt");
    }

    [TestMethod]
    public void ExtractTarContents_SkipsEntryProgressForCompletionOnlyTask()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        CreateTarWithPermissions(tarPath,
            ("hello.txt", defaultMode, isDirectory: false),
            ("sub/nested.txt", defaultMode, isDirectory: false));

        var task = new TestProgressTask(requiresKnownMaximum: false)
        {
            MaxValue = 17,
            Value = 3,
        };

        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, task);

        task.MaxValue.Should().Be(17);
        task.Value.Should().Be(3);
        File.Exists(Path.Combine(extractDir, "hello.txt")).Should().BeTrue();
        File.Exists(Path.Combine(extractDir, "sub", "nested.txt")).Should().BeTrue();
    }

    [TestMethod]
    public void ExtractTarContents_ReportsEntryProgressWhenMaximumIsRequired()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        CreateTarWithPermissions(tarPath,
            ("hello.txt", defaultMode, isDirectory: false),
            ("sub/nested.txt", defaultMode, isDirectory: false));

        var task = new TestProgressTask(requiresKnownMaximum: true);

        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, task);

        task.MaxValue.Should().Be(2);
        task.Value.Should().Be(2);
    }

    [TestMethod]
    public void ExtractTarContents_SkipsExistingSubcomponents()
    {
        // Arrange — create a tar with files in two subcomponents
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

        CreateTarWithPermissions(tarPath,
            ("shared/Microsoft.NETCore.App/10.0.1/System.dll", defaultMode, isDirectory: false),
            ("shared/Microsoft.NETCore.App/10.0.1/coreclr.dll", defaultMode, isDirectory: false),
            ("sdk/10.0.103/dotnet.dll", defaultMode, isDirectory: false));

        // Pre-create the shared runtime directory to simulate existing installation
        var existingDir = Path.Combine(extractDir, "shared", "Microsoft.NETCore.App", "10.0.1");
        Directory.CreateDirectory(existingDir);
        File.WriteAllText(Path.Combine(existingDir, "System.dll"), "original content");

        var extractedEntries = new List<string>();

        // Skip entries whose subcomponent directory already exists on disk
        Func<string, bool> shouldSkip = entryName =>
        {
            var subcomponent = SubcomponentResolver.Resolve(entryName);
            if (subcomponent is null)
            {
                return false;
            }

            var subPath = Path.Combine(extractDir, subcomponent.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(subPath);
        };

        // Act
        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, installTask: null,
            muxerHandler: null, onEntryExtracted: entry => extractedEntries.Add(entry), shouldSkipEntry: shouldSkip);

        // Assert — existing subcomponent content should be preserved (not overwritten)
        File.ReadAllText(Path.Combine(existingDir, "System.dll")).Should().Be("original content",
            "files in existing subcomponents should not be overwritten");

        // The second file in the existing subcomponent should NOT have been created
        File.Exists(Path.Combine(existingDir, "coreclr.dll")).Should().BeFalse(
            "new files in existing subcomponents should not be extracted");

        // New subcomponent should be extracted
        var sdkFile = Path.Combine(extractDir, "sdk", "10.0.103", "dotnet.dll");
        File.Exists(sdkFile).Should().BeTrue("new subcomponent files should be extracted");

        // All entries should still be reported to the callback for manifest tracking
        extractedEntries.Should().HaveCount(3);
    }

    [TestMethod]
    public void ExtractTarContents_RootLevelFilesAlwaysExtracted()
    {
        // Arrange — root-level files (not part of any subcomponent) should always be extracted
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var tarPath = Path.Combine(testEnv.TempRoot, "test.tar");
        var extractDir = Path.Combine(testEnv.TempRoot, "extracted");
        Directory.CreateDirectory(extractDir);

        var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

        CreateTarWithPermissions(tarPath,
            ("LICENSE.txt", defaultMode, isDirectory: false),
            ("shared/Microsoft.NETCore.App/10.0.1/System.dll", defaultMode, isDirectory: false));

        // Pre-create the shared runtime directory
        var existingDir = Path.Combine(extractDir, "shared", "Microsoft.NETCore.App", "10.0.1");
        Directory.CreateDirectory(existingDir);

        // Skip predicate that skips existing subcomponents
        Func<string, bool> shouldSkip = entryName =>
        {
            var subcomponent = SubcomponentResolver.Resolve(entryName);
            if (subcomponent is null)
            {
                return false;
            }

            var subPath = Path.Combine(extractDir, subcomponent.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(subPath);
        };

        // Act
        DotnetArchiveExtractor.ExtractTarArchive(tarPath, extractDir, installTask: null,
            muxerHandler: null, onEntryExtracted: null, shouldSkipEntry: shouldSkip);

        // Assert — root-level file should be extracted even though skip predicate is active
        File.Exists(Path.Combine(extractDir, "LICENSE.txt")).Should().BeTrue(
            "root-level files should always be extracted");

        // Subcomponent file should be skipped
        File.Exists(Path.Combine(existingDir, "System.dll")).Should().BeFalse(
            "files in existing subcomponents should be skipped");
    }

    [TestMethod]
    public void Commit_ExtractsTarGzArchive_Correctly()
    {
        // Arrange — create a real tar.gz archive and verify the full Prepare → Commit path
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        byte[] tarGzContent = CreateTarGzWithFiles(
            ("shared/Microsoft.NETCore.App/9.0.0/System.Runtime.dll", "runtime-content"),
            ("dotnet" + DotnetupUtilities.ExeSuffix, "muxer-content"));

        var mockDownloader = new MockArchiveDownloader
        {
            CreateFakeArchive = true,
            FakeArchiveContent = tarGzContent,
            ArchiveFileExtension = ".tar.gz"
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        // Act
        extractor.Prepare();
        extractor.Commit();

        // Assert
        var runtimeFile = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "9.0.0", "System.Runtime.dll");
        File.Exists(runtimeFile).Should().BeTrue("tar.gz extraction should create files in the install path");
        File.ReadAllText(runtimeFile).Should().Be("runtime-content");
    }

    [TestMethod]
    public void Commit_ExtractsUncompressedTarArchive_Correctly()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);
        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        using var tarStream = new MemoryStream();
        using (var writer = new TarWriter(tarStream, leaveOpen: true))
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "shared/Microsoft.NETCore.App/9.0.0/System.Runtime.dll")
            {
                DataStream = new MemoryStream("runtime-content"u8.ToArray()),
            });
        }

        var mockDownloader = new MockArchiveDownloader
        {
            FakeArchiveContent = tarStream.ToArray(),
            ArchiveFileExtension = ".tar"
        };

        using var extractor = new DotnetArchiveExtractor(
            request, version, new ReleaseManifest(), new NullProgressTarget(), mockDownloader);

        extractor.Prepare();
        extractor.Commit();

        string runtimeFile = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "9.0.0", "System.Runtime.dll");
        File.ReadAllText(runtimeFile).Should().Be("runtime-content");
    }

    [TestMethod]
    public void Commit_ExtractsTarGzArchive_WhenDecompressedTarPathAlreadyExists()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        byte[] tarGzContent = CreateTarGzWithFiles(
            ("shared/Microsoft.NETCore.App/9.0.0/System.Runtime.dll", "runtime-content"));

        var mockDownloader = new MockArchiveDownloader
        {
            CreateFakeArchive = true,
            FakeArchiveContent = tarGzContent,
            ArchiveFileExtension = ".tar.gz"
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        extractor.Prepare();
        var downloadedPath = mockDownloader.DownloadCalls[0].DestinationPath;
        var decompressedTarPath = Path.Combine(Path.GetDirectoryName(downloadedPath)!, Path.GetFileNameWithoutExtension(downloadedPath));

        // Pre-create a directory at the path where the old implementation would have placed
        // the decompressed .tar staging file. This verifies that the current streaming approach
        // does not depend on that path being available as a regular file.
        Directory.CreateDirectory(decompressedTarPath);
        extractor.Commit();

        var runtimeFile = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "9.0.0", "System.Runtime.dll");
        File.Exists(runtimeFile).Should().BeTrue("tar.gz extraction should not depend on a sibling decompressed .tar staging file");
        File.ReadAllText(runtimeFile).Should().Be("runtime-content");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Commit_ExtractsZipArchive_Correctly()
    {
        // Arrange — create a real zip archive and verify the full Prepare → Commit path
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        byte[] zipContent = CreateZipWithFiles(
            ("shared/Microsoft.NETCore.App/9.0.0/System.Runtime.dll", "runtime-content"),
            ("dotnet" + DotnetupUtilities.ExeSuffix, "muxer-content"));

        var mockDownloader = new MockArchiveDownloader
        {
            CreateFakeArchive = true,
            FakeArchiveContent = zipContent,
            ArchiveFileExtension = ".zip"
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        // Act
        extractor.Prepare();
        extractor.Commit();

        // Assert
        var runtimeFile = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "9.0.0", "System.Runtime.dll");
        File.Exists(runtimeFile).Should().BeTrue("zip extraction should create files in the install path");
        File.ReadAllText(runtimeFile).Should().Be("runtime-content");
    }

    /// <summary>
    /// Creates a tar.gz archive in memory with the specified file entries.
    /// </summary>
    private static byte[] CreateTarGzWithFiles(params (string path, string content)[] files)
    {
        using var ms = new MemoryStream();
        using (var gzStream = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        using (var writer = new TarWriter(gzStream))
        {
            foreach (var (path, content) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
                };
                writer.WriteEntry(entry);
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Creates a zip archive in memory with the specified file entries.
    /// </summary>
    private static byte[] CreateZipWithFiles(params (string path, string content)[] files)
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = zip.CreateEntry(path);
                using var stream = entry.Open();
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        return ms.ToArray();
    }

    private sealed class TestProgressTask(bool requiresKnownMaximum) : IProgressTask
    {
        public string Description { get; set; } = string.Empty;
        public double Value { get; set; }
        public double MaxValue { get; set; }
        public bool RequiresKnownMaximum { get; } = requiresKnownMaximum;
    }
}
