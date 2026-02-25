// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class GarbageCollectorTests
{
    private static string CreateTempManifestPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotnetup-gc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "manifest.json");
    }

    private static string CreateTempDotnetRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotnetup-gc-tests", Guid.NewGuid().ToString("N"), "dotnet");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(params string[] paths)
    {
        foreach (var path in paths)
        {
            var dir = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
            // Walk up to the test-specific GUID directory
            while (dir != null && Directory.Exists(dir) && !dir.EndsWith("dotnetup-gc-tests"))
            {
                var parent = Path.GetDirectoryName(dir);
                try { Directory.Delete(dir, true); } catch { }
                dir = parent;
            }
        }
    }

    [Fact]
    public void RemovesUnreferencedInstallationRecords()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

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
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.102"));
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.103"));

            // GC should keep 10.0.103 (latest matching "10") and remove 10.0.102
            var gc = new GarbageCollector(manifest);
            var deleted = gc.Collect(installRoot);

            // The old version should have been removed from manifest
            var installations = manifest.GetInstallations(installRoot).ToList();
            installations.Should().ContainSingle();
            installations[0].Version.Should().Be("10.0.103");

            // The old version's folder should have been deleted from disk
            deleted.Should().Contain("sdk/10.0.102");
            Directory.Exists(Path.Combine(dotnetRoot, "sdk", "10.0.102")).Should().BeFalse();
            Directory.Exists(Path.Combine(dotnetRoot, "sdk", "10.0.103")).Should().BeTrue();
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }

    [Fact]
    public void KeepsInstallationReferencedByMultipleSpecs()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

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

            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.103"));

            var gc = new GarbageCollector(manifest);
            var deleted = gc.Collect(installRoot);

            deleted.Should().BeEmpty();
            manifest.GetInstallations(installRoot).Should().ContainSingle();
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }

    [Fact]
    public void RemovesStaleGlobalJsonSpecs()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

            // Add a global.json spec pointing to a non-existent file
            manifest.AddInstallSpec(installRoot, new InstallSpec
            {
                Component = InstallComponent.SDK,
                VersionOrChannel = "10.0.100",
                InstallSource = InstallSource.GlobalJson,
                GlobalJsonPath = Path.Combine(dotnetRoot, "nonexistent", "global.json")
            });

            manifest.AddInstallation(installRoot, new Installation
            {
                Component = InstallComponent.SDK,
                Version = "10.0.100",
                Subcomponents = ["sdk/10.0.100"]
            });

            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.100"));

            var gc = new GarbageCollector(manifest);
            var deleted = gc.Collect(installRoot);

            // The stale spec should have been removed, and with it, the installation
            manifest.GetInstallSpecs(installRoot).Should().BeEmpty();
            manifest.GetInstallations(installRoot).Should().BeEmpty();
            deleted.Should().Contain("sdk/10.0.100");
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }

    [Fact]
    public void VersionMatchingWorksForChannelPatterns()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

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

            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.103"));
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.204"));

            var gc = new GarbageCollector(manifest);
            var deleted = gc.Collect(installRoot);

            var installations = manifest.GetInstallations(installRoot).ToList();
            installations.Should().ContainSingle();
            installations[0].Version.Should().Be("10.0.103");
            deleted.Should().Contain("sdk/10.0.204");
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }
}
