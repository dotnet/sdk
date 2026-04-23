// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class InitWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public InitWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe for parallel test execution.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    // ── ShouldPromptToConvertSystemInstalls ──

    [Fact]
    public void ShouldReplaceSystemConfiguration_ReturnsFalse_ForDotnetupDotnet()
    {
        InitWorkflows.ShouldReplaceSystemConfiguration(PathPreference.DotnetupDotnet)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PathPreference.FullPathReplacement)]
    internal void ShouldReplaceSystemConfiguration_ReturnsTrue_ForPathReplacingModes(PathPreference preference)
    {
        InitWorkflows.ShouldReplaceSystemConfiguration(preference)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsFalse_ForDotnetupDotnet()
    {
        InitWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.DotnetupDotnet)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PathPreference.ShellProfile)]
    [InlineData(PathPreference.FullPathReplacement)]
    internal void ShouldPromptToConvertSystemInstalls_ReturnsTrue_ForNonIsolationModes(PathPreference preference)
    {
        InitWorkflows.ShouldPromptToConvertSystemInstalls(preference)
            .Should().BeTrue();
    }

    // ── PromptInstallsToMigrateIfDesired — early-exit paths ──

    [Fact]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenPreferenceIsDotnetupDotnet()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.DotnetupDotnet, installRoot);

        result.Should().BeEmpty();
        // GetExistingSystemInstalls should not be called when ShouldPromptToConvertSystemInstalls is false
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    [Fact]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenNoSystemInstallsExist()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);

        string manifestPath = Path.Combine(_tempDir, "manifest.json");
        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.ShellProfile, installRoot, manifestPath);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    [Fact]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenInteractiveIsFalse()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock,
            PathPreference.ShellProfile,
            installRoot,
            interactive: false);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
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

        var result = InitWorkflows.GetMigrationCandidates(
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

        var result = InitWorkflows.GetMigrationCandidates(
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

        var result = InitWorkflows.BuildMigrationSelections(systemInstalls, installRoot, existingRequests: existingRequests);

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

        var result = InitWorkflows.BuildMigrationSelections(systemInstalls, installRoot, existingRequests: existingRequests);

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

        var result = InitWorkflows.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);

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

        var result = InitWorkflows.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);

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

        var result = InitWorkflows.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);

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

        var toMigrate = InitWorkflows.BuildMigrationSelections(systemInstalls, installRoot, existingRequests: existingRequests);
        var result = InitWorkflows.MergeInstallRequests(existingRequests, toMigrate, installRoot);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Request.Component == InstallComponent.SDK && r.Request.Channel.Name == "10.0.1xx");
        result.Should().ContainSingle(r => r.Request.Component == InstallComponent.Runtime && r.Request.Channel.Name == "10.0" && r.ResolvedVersion.ToString() == "10.0.4");
    }

    // ── GetExistingSystemInstalls — architecture filtering ──

    [Fact]
    public void GetExistingSystemInstalls_FiltersToNativeArchOnly()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var foreignArch = nativeArch == InstallArchitecture.x64 ? InstallArchitecture.arm64 : InstallArchitecture.x64;

        var nativeRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var foreignRoot = new DotnetInstallRoot(_tempDir, foreignArch);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(nativeRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(foreignRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(nativeRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
                new DotnetInstall(foreignRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime),
            ]);

        var result = mock.GetExistingSystemInstalls();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.InstallRoot.Architecture == nativeArch);
        // Verify descending sort order from the real filter
        result[0].Version.ToString().Should().BeOneOf("10.0.100", "10.0.0");
        result[0].Version.CompareTo(result[1].Version).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetExistingSystemInstalls_DeduplicatesSameComponentVersionAndArch()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("8.0.22"), InstallComponent.Runtime),
                new DotnetInstall(installRoot, new ReleaseVersion("8.0.22"), InstallComponent.Runtime),
            ]);

        var result = mock.GetExistingSystemInstalls();

        result.Should().HaveCount(2);
        result.Should().ContainSingle(i => i.Component == InstallComponent.SDK && i.Version.ToString() == "10.0.100");
        result.Should().ContainSingle(i => i.Component == InstallComponent.Runtime && i.Version.ToString() == "8.0.22");
    }

    [Fact]
    public void FormatMigrationDisplayItems_IncludesArchitecture_WhenMultipleArchitecturesArePresent()
    {
        List<InitWorkflows.MigrationSelection> migrationSelections =
        [
            new(InstallComponent.SDK, new UpdateChannel("10.0.1xx"), new ReleaseVersion("10.0.100"), InstallArchitecture.x64),
            new(InstallComponent.SDK, new UpdateChannel("10.0.1xx"), new ReleaseVersion("10.0.100"), InstallArchitecture.arm64),
        ];

        var items = InitWorkflows.FormatMigrationDisplayItems(migrationSelections);

        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.Contains("10.0.1xx") && i.Contains("["));
    }


}
