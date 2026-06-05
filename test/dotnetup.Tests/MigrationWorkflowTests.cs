// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void BuildMigrationSelections_DeduplicatesChannelsAndSkipsExistingSpecs()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        List<DotnetInstall> systemInstalls =
        [
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.101"), InstallComponent.SDK),
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.4"), InstallComponent.Runtime),
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
            new DotnetInstall(installRoot, new ReleaseVersion("9.0.5"), InstallComponent.ASPNETCore),
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

        var result = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, existingRequests: existingRequests);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Component == InstallComponent.SDK && r.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Component == InstallComponent.ASPNETCore && r.Channel.Name == "9.0");
    }

    [Fact]
    public void BuildMigrationSelections_UsesInstallSpecChannelsForExistingRequests()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        List<DotnetInstall> systemInstalls =
        [
            new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            new DotnetInstall(installRoot, new ReleaseVersion("9.0.306"), InstallComponent.SDK),
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

        var result = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, existingRequests: existingRequests);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Component == InstallComponent.SDK && r.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Component == InstallComponent.SDK && r.Channel.Name == "9.0.3xx");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

        var toMigrate = MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, existingRequests: existingRequests);
        var result = MigrationWorkflow.MergeInstallRequests(existingRequests, toMigrate, installRoot);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Request.Component == InstallComponent.SDK && r.Request.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Request.Component == InstallComponent.Runtime && r.Request.Channel.Name == "10.0" && r.ResolvedVersion.ToString() == "10.0.4");
    }
}
