// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class GarbageCollectorTests
{
    [Fact]
    public void RemovesUnreferencedInstallationRecords()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        // Add a spec for channel "10" and two installations
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "10",
            InstallSource = InstallSource.Explicit
        });

        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.102",
            Subcomponents = ["sdk/10.0.102"]
        });

        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.103",
            Subcomponents = ["sdk/10.0.103"]
        });

        // Create both sdk dirs on disk
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.102"));
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.103"));

        // GC should keep 10.0.103 (latest matching "10") and remove 10.0.102
        var gc = new GarbageCollector(manifest);
        var deleted = gc.Collect(installRoot);

        // The old version should have been removed from manifest
        var installations = manifest.GetInstallations(installRoot).ToList();
        installations.Should().ContainSingle();
        installations[0].Version.Should().Be("10.0.103");

        // The old version's folder should have been deleted from disk
        deleted.Should().Contain("sdk/10.0.102");
        Directory.Exists(Path.Combine(testEnv.InstallPath, "sdk", "10.0.102")).Should().BeFalse();
        Directory.Exists(Path.Combine(testEnv.InstallPath, "sdk", "10.0.103")).Should().BeTrue();
    }

    [Fact]
    public void KeepsInstallationReferencedByMultipleSpecs()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        // Two specs that both match the same installation
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "10",
            InstallSource = InstallSource.Explicit
        });
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "10.0.103",
            InstallSource = InstallSource.Explicit
        });

        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.103",
            Subcomponents = ["sdk/10.0.103"]
        });

        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.103"));

        var gc = new GarbageCollector(manifest);
        var deleted = gc.Collect(installRoot);

        deleted.Should().BeEmpty();
        manifest.GetInstallations(installRoot).Should().ContainSingle();
    }

    [Fact]
    public void RemovesStaleGlobalJsonSpecs()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        // Add a global.json spec pointing to a non-existent file
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "10.0.100",
            InstallSource = InstallSource.GlobalJson,
            GlobalJsonPath = Path.Combine(testEnv.InstallPath, "nonexistent", "global.json")
        });

        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.100",
            Subcomponents = ["sdk/10.0.100"]
        });

        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.100"));

        var gc = new GarbageCollector(manifest);
        var deleted = gc.Collect(installRoot);

        // The stale spec should have been removed, and with it, the installation
        manifest.GetInstallSpecs(installRoot).Should().BeEmpty();
        manifest.GetInstallations(installRoot).Should().BeEmpty();
        deleted.Should().Contain("sdk/10.0.100");
    }

    [Fact]
    public void VersionMatchingWorksForExplicitChannelPatterns()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        // Feature band spec: "10.0.1xx"
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "10.0.1xx",
            InstallSource = InstallSource.Explicit
        });

        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.103",
            Subcomponents = ["sdk/10.0.103"]
        });

        // This installation is in a different feature band — should be removed
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.204",
            Subcomponents = ["sdk/10.0.204"]
        });

        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.103"));
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.204"));

        var gc = new GarbageCollector(manifest);
        var deleted = gc.Collect(installRoot);

        var installations = manifest.GetInstallations(installRoot).ToList();
        installations.Should().ContainSingle();
        installations[0].Version.Should().Be("10.0.103");
        deleted.Should().Contain("sdk/10.0.204");
    }
}
