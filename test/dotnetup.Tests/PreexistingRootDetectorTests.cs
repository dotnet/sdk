// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class PreexistingRootDetectorTests
{
    private static string CreateTempManifestPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotnetup-preexist-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "manifest.json");
    }

    private static string CreateTempDotnetRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotnetup-preexist-tests", Guid.NewGuid().ToString("N"), "dotnet");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(params string[] paths)
    {
        foreach (var path in paths)
        {
            var dir = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
            while (dir != null && Directory.Exists(dir) && !dir.EndsWith("dotnetup-preexist-tests"))
            {
                var parent = Path.GetDirectoryName(dir);
                try { Directory.Delete(dir, true); } catch { }
                dir = parent;
            }
        }
    }

    [Fact]
    public void DetectsPreexistingSdks()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            // Create existing SDK directories
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "9.0.103"));
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.100"));

            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

            PreexistingRootDetector.EnsureRootIsTracked(manifest, installRoot);

            var specs = manifest.GetInstallSpecs(installRoot).ToList();
            specs.Should().HaveCount(2);
            specs.Should().Contain(s => s.VersionOrChannel == "9.0.103" && s.InstallSource == InstallSource.Previous);
            specs.Should().Contain(s => s.VersionOrChannel == "10.0.100" && s.InstallSource == InstallSource.Previous);

            var installations = manifest.GetInstallations(installRoot).ToList();
            installations.Should().HaveCount(2);
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }

    [Fact]
    public void DetectsPreexistingRuntimes()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App", "9.0.12"));
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "shared", "Microsoft.AspNetCore.App", "9.0.12"));

            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

            PreexistingRootDetector.EnsureRootIsTracked(manifest, installRoot);

            var specs = manifest.GetInstallSpecs(installRoot).ToList();
            specs.Should().HaveCount(2);
            specs.Should().Contain(s => s.Component == InstallComponent.Runtime);
            specs.Should().Contain(s => s.Component == InstallComponent.ASPNETCore);

            var installations = manifest.GetInstallations(installRoot).ToList();
            installations.Should().HaveCount(2);
            installations.Should().Contain(i => i.Component == InstallComponent.Runtime && i.Version == "9.0.12");
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }

    [Fact]
    public void DoesNotRunIfRootAlreadyTracked()
    {
        var manifestPath = CreateTempManifestPath();
        var dotnetRoot = CreateTempDotnetRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "9.0.103"));
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", "10.0.100"));

            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(dotnetRoot, InstallArchitecture.x64);

            // Add one spec — root is now tracked
            manifest.AddInstallSpec(installRoot, new InstallSpec
            {
                Component = InstallComponent.SDK,
                VersionOrChannel = "latest",
                InstallSource = InstallSource.Explicit
            });

            PreexistingRootDetector.EnsureRootIsTracked(manifest, installRoot);

            // Should NOT have added specs for existing SDKs since root is already tracked
            var specs = manifest.GetInstallSpecs(installRoot).ToList();
            specs.Should().ContainSingle();
            specs[0].VersionOrChannel.Should().Be("latest");
        }
        finally
        {
            Cleanup(manifestPath, dotnetRoot);
        }
    }

    [Fact]
    public void SkipsNonExistentRoot()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "nonexistent-root-" + Guid.NewGuid()), InstallArchitecture.x64);

            // Should not throw
            PreexistingRootDetector.EnsureRootIsTracked(manifest, installRoot);

            manifest.GetInstallSpecs(installRoot).Should().BeEmpty();
        }
        finally
        {
            Cleanup(manifestPath);
        }
    }
}
