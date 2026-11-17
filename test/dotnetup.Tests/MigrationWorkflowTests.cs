// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class MigrationWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public MigrationWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-migration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe for parallel test execution.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [TestMethod]
    public void GetMigrationCandidates_CanFilterToRequestedComponents()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK);
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: [sdkInstall, runtimeInstall]);

        var result = MigrationWorkflow.GetMigrationCandidates(
            mock,
            components: [InstallComponent.SDK]);

        result.Should().Equal([sdkInstall]);
    }

    [TestMethod]
    public void GetMigrationCandidates_CanFilterToRuntimeFamily()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK);
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime);
        var aspNetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.5"), InstallComponent.ASPNETCore);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: [sdkInstall, runtimeInstall, aspNetInstall]);

        var result = MigrationWorkflow.GetMigrationCandidates(
            mock,
            components: [InstallComponent.Runtime, InstallComponent.ASPNETCore, InstallComponent.WindowsDesktop]);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Component != InstallComponent.SDK);
        result.Should().Contain(i => i.Component == InstallComponent.Runtime);
        result.Should().Contain(i => i.Component == InstallComponent.ASPNETCore);
    }

    [TestMethod]
    public void FilterMigrationSelections_ExcludesPendingInstallSpecs()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        List<MigrationWorkflow.MigrationSelection> candidates =
        [
            CreateMigration(InstallComponent.SDK, "10.0.1xx", "10.0.101"),
            CreateMigration(InstallComponent.Runtime, "10.0", "10.0.4"),
            CreateMigration(InstallComponent.ASPNETCore, "9.0", "9.0.5"),
        ];

        List<ResolvedInstallRequest> existingRequests =
        [
            new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    new UpdateChannel("10.0"),
                    InstallComponent.Runtime,
                    new InstallRequestOptions()),
                new ReleaseVersion("10.0.5")),
        ];

        var result = MigrationWorkflow.FilterMigrationSelections(candidates, existingRequests);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Component == InstallComponent.SDK && r.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Component == InstallComponent.ASPNETCore && r.Channel.Name == "9.0");
    }

    [TestMethod]
    public void FilterMigrationSelections_UsesInstallSpecChannels()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        List<MigrationWorkflow.MigrationSelection> candidates =
        [
            CreateMigration(InstallComponent.SDK, "10.0.1xx", "10.0.100"),
            CreateMigration(InstallComponent.SDK, "9.0.3xx", "9.0.306"),
        ];

        List<ResolvedInstallRequest> existingRequests =
        [
            new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    new UpdateChannel(ChannelVersionResolver.LatestChannel),
                    InstallComponent.SDK,
                    new InstallRequestOptions()),
                new ReleaseVersion("10.0.100")),
        ];

        var result = MigrationWorkflow.FilterMigrationSelections(candidates, existingRequests);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Component == InstallComponent.SDK && r.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Component == InstallComponent.SDK && r.Channel.Name == "9.0.3xx");
    }

    [TestMethod]
    public void BuildMigrationSelections_ExcludesChannelsAlreadyTrackedInManifest()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        string manifestPath = Path.Combine(_tempDir, "manifest.json");

        using (new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            manifest.WriteManifest(new DotnetupManifestData
            {
                DotnetRoots =
                [
                    new DotnetRootEntry
                    {
                        Path = installRoot.Path,
                        Architecture = installRoot.Architecture,
                        InstallSpecs =
                        [
                            new InstallSpec { Component = InstallComponent.SDK, VersionOrChannel = "10.0.1xx" },
                            new InstallSpec { Component = InstallComponent.Runtime, VersionOrChannel = "10.0" },
                        ],
                    },
                ],
            });
        }

        List<DotnetInstall> systemInstalls =
        [
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.101"), InstallComponent.SDK),
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.4"), InstallComponent.Runtime),
            new DotnetInstall(installRoot, new ReleaseVersion("9.0.5"), InstallComponent.ASPNETCore),
        ];

        var result = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);

        result.Should().ContainSingle();
        result[0].Component.Should().Be(InstallComponent.ASPNETCore);
        result[0].Channel.Name.Should().Be("9.0");
    }

    [TestMethod]
    public void BuildMigrationSelections_TreatsRuntimeChannels9And90AsEquivalent()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        string manifestPath = Path.Combine(_tempDir, "manifest.json");

        using (new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            manifest.WriteManifest(new DotnetupManifestData
            {
                DotnetRoots =
                [
                    new DotnetRootEntry
                    {
                        Path = installRoot.Path,
                        Architecture = installRoot.Architecture,
                        InstallSpecs =
                        [
                            new InstallSpec { Component = InstallComponent.Runtime, VersionOrChannel = "9" },
                        ],
                    },
                ],
            });
        }

        List<DotnetInstall> systemInstalls =
        [
            new DotnetInstall(installRoot, new ReleaseVersion("9.0.5"), InstallComponent.Runtime),
        ];

        var result = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void BuildMigrationSelections_DoesNotUseTrackedInstallationsForChannelExclusion()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        string manifestPath = Path.Combine(_tempDir, "manifest.json");

        using (new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            manifest.WriteManifest(new DotnetupManifestData
            {
                DotnetRoots =
                [
                    new DotnetRootEntry
                    {
                        Path = installRoot.Path,
                        Architecture = installRoot.Architecture,
                        Installations =
                        [
                            new Installation { Component = InstallComponent.Runtime, Version = "10.0.4" },
                        ],
                    },
                ],
            });
        }

        List<DotnetInstall> systemInstalls =
        [
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.4"), InstallComponent.Runtime),
        ];

        var result = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);

        result.Should().ContainSingle();
        result[0].Component.Should().Be(InstallComponent.Runtime);
        result[0].Channel.Name.Should().Be("10.0");
    }

    [TestMethod]
    public void MergeInstallRequests_AddsMigrationRequestsWithoutDuplicatingExistingChannels()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        List<ResolvedInstallRequest> existingRequests =
        [
            new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    new UpdateChannel("10.0.1xx"),
                    InstallComponent.SDK,
                    new InstallRequestOptions()),
                new ReleaseVersion("10.0.100")),
        ];

        List<DotnetInstall> systemInstalls =
        [
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.101"), InstallComponent.SDK),
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.4"), InstallComponent.Runtime),
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
        ];

        var toMigrate = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot);
        var result = MigrationWorkflow.MergeInstallRequests(existingRequests, toMigrate, installRoot);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Request.Component == InstallComponent.SDK && r.Request.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Request.Component == InstallComponent.Runtime && r.Request.Channel.Name == "10.0" && r.ResolvedVersion.ToString() == "10.0.4");
        result.Single(r => r.Request.Component == InstallComponent.SDK).Request.Options.InstallSource.Should().Be(InstallSource.Explicit);
        result.Single(r => r.Request.Component == InstallComponent.Runtime).Request.Options.InstallSource.Should().Be(InstallSource.Migration);
    }

    [TestMethod]
    public void MergeInstallRequests_RecordsMigrationSourceForEveryComponent()
    {
        var installRoot = new DotnetInstallRoot(_tempDir, InstallerUtilities.GetDefaultInstallArchitecture());
        string manifestPath = Path.Combine(_tempDir, "manifest.json");
        var primaryRequest = new ResolvedInstallRequest(
            new DotnetInstallRequest(installRoot, new UpdateChannel("9.0.1xx"), InstallComponent.SDK,
                new InstallRequestOptions
                {
                    InstallSource = InstallSource.GlobalJson,
                    GlobalJsonPath = Path.Combine(_tempDir, "global.json"),
                    ManifestPath = manifestPath
                }),
            new ReleaseVersion("9.0.100"));
        List<MigrationWorkflow.MigrationSelection> migrations =
        [
            CreateMigration(InstallComponent.SDK, "10.0.1xx", "10.0.100"),
            CreateMigration(InstallComponent.Runtime, "10.0", "10.0.5"),
            CreateMigration(InstallComponent.ASPNETCore, "10.0", "10.0.5"),
            CreateMigration(InstallComponent.WindowsDesktop, "10.0", "10.0.5"),
        ];

        var requests = MigrationWorkflow.MergeInstallRequests([primaryRequest], migrations, installRoot, manifestPath: manifestPath);
        requests[0].Should().BeSameAs(primaryRequest);
        requests.Skip(1).Should().OnlyContain(r => r.Request.Options.InstallSource == InstallSource.Migration
            && r.Request.Options.GlobalJsonPath == null && r.Request.Options.ManifestPath == manifestPath);

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(manifestPath);
        foreach (var request in requests)
        {
            manifest.RecordInstallSpec(request.Request);
        }

        var specs = new DotnetupSharedManifest(manifestPath).GetInstallSpecs(installRoot).ToList();
        specs.Should().HaveCount(5);
        specs.Single(s => s.VersionOrChannel == "9.0.1xx").InstallSource.Should().Be(InstallSource.GlobalJson);
        specs.Where(s => s.InstallSource == InstallSource.Migration).Select(s => s.Component)
            .Should().BeEquivalentTo(migrations.Select(m => m.Component));
        MigrationWorkflow.BuildMigrationSelections(
            [new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK)],
            installRoot, manifestPath).Should().BeEmpty();
    }

    private static MigrationWorkflow.MigrationSelection CreateMigration(
        InstallComponent component,
        string channel,
        string version)
    {
        return new MigrationWorkflow.MigrationSelection(
            component,
            new UpdateChannel(channel),
            new ReleaseVersion(version),
            InstallerUtilities.GetDefaultInstallArchitecture());
    }
}
