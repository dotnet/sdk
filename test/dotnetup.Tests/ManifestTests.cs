// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ManifestTests
{
    private static string CreateTempManifestPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotnetup-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "manifest.json");
    }

    private static void CleanupManifest(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void NewManifestCreatesValidJson()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);

            var json = File.ReadAllText(manifestPath);
            var data = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.DotnetupManifestData);

            data.Should().NotBeNull();
            data!.SchemaVersion.Should().Be("1");
            data.DotnetRoots.Should().BeEmpty();
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }

    [Fact]
    public void SerializationRoundTrip()
    {
        var original = new DotnetupManifestData
        {
            SchemaVersion = "1",
            DotnetRoots =
            [
                new DotnetRootEntry
                {
                    Path = @"C:\Users\Test\AppData\Local\dotnet",
                    Architecture = InstallArchitecture.x64,
                    InstallSpecs =
                    [
                        new InstallSpec
                        {
                            Component = InstallComponent.SDK,
                            VersionOrChannel = "10",
                            InstallSource = InstallSource.Explicit
                        },
                        new InstallSpec
                        {
                            Component = InstallComponent.Runtime,
                            VersionOrChannel = "9",
                            InstallSource = InstallSource.Explicit
                        }
                    ],
                    Installations =
                    [
                        new Installation
                        {
                            Component = InstallComponent.SDK,
                            Version = "10.0.103",
                            Subcomponents = ["sdk/10.0.103", "shared/Microsoft.NETCore.App/10.0.3"]
                        },
                        new Installation
                        {
                            Component = InstallComponent.Runtime,
                            Version = "9.0.12",
                            Subcomponents = ["shared/Microsoft.NETCore.App/9.0.12"]
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(original, DotnetupManifestJsonContext.Default.DotnetupManifestData);
        var deserialized = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.DotnetupManifestData);

        deserialized.Should().NotBeNull();
        deserialized!.SchemaVersion.Should().Be("1");
        deserialized.DotnetRoots.Should().HaveCount(1);

        var root = deserialized.DotnetRoots[0];
        root.Path.Should().Be(@"C:\Users\Test\AppData\Local\dotnet");
        root.Architecture.Should().Be(InstallArchitecture.x64);
        root.InstallSpecs.Should().HaveCount(2);
        root.Installations.Should().HaveCount(2);

        root.InstallSpecs[0].Component.Should().Be(InstallComponent.SDK);
        root.InstallSpecs[0].VersionOrChannel.Should().Be("10");
        root.InstallSpecs[0].InstallSource.Should().Be(InstallSource.Explicit);

        root.Installations[0].Component.Should().Be(InstallComponent.SDK);
        root.Installations[0].Version.Should().Be("10.0.103");
        root.Installations[0].Subcomponents.Should().Contain("sdk/10.0.103");
    }

    [Fact]
    public void MigratesLegacyFormat()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

            // Write old-format manifest (flat list of DotnetInstall)
            var legacyInstalls = new List<DotnetInstall>
            {
                new(new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64), new ReleaseVersion("9.0.103"), InstallComponent.SDK)
            };
            var legacyJson = JsonSerializer.Serialize(legacyInstalls, DotnetupManifestJsonContext.Default.ListDotnetInstall);
            File.WriteAllText(manifestPath, legacyJson);

            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);

            // Should read and migrate the legacy format
            var installs = manifest.GetInstalledVersions().ToList();
            installs.Should().ContainSingle();
            installs[0].Version.ToString().Should().Be("9.0.103");
            installs[0].Component.Should().Be(InstallComponent.SDK);

            // After migration, the file should be in new format
            var json = File.ReadAllText(manifestPath);
            var data = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.DotnetupManifestData);
            data.Should().NotBeNull();
            data!.SchemaVersion.Should().Be("1");
            data.DotnetRoots.Should().ContainSingle();
            data.DotnetRoots[0].Installations.Should().ContainSingle();
            data.DotnetRoots[0].InstallSpecs.Should().ContainSingle();
            data.DotnetRoots[0].InstallSpecs[0].InstallSource.Should().Be(InstallSource.Previous);
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }

    [Fact]
    public void AddAndRemoveInstallSpec()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

            var spec = new InstallSpec
            {
                Component = InstallComponent.SDK,
                VersionOrChannel = "10",
                InstallSource = InstallSource.Explicit
            };

            manifest.AddInstallSpec(installRoot, spec);
            manifest.GetInstallSpecs(installRoot).Should().ContainSingle();

            // Adding duplicate should not create a second entry
            manifest.AddInstallSpec(installRoot, spec);
            manifest.GetInstallSpecs(installRoot).Should().ContainSingle();

            manifest.RemoveInstallSpec(installRoot, spec);
            manifest.GetInstallSpecs(installRoot).Should().BeEmpty();
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }

    [Fact]
    public void AddAndRemoveInstallation()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

            var installation = new Installation
            {
                Component = InstallComponent.SDK,
                Version = "10.0.103",
                Subcomponents = ["sdk/10.0.103", "shared/Microsoft.NETCore.App/10.0.3"]
            };

            manifest.AddInstallation(installRoot, installation);
            var installations = manifest.GetInstallations(installRoot).ToList();
            installations.Should().ContainSingle();
            installations[0].Version.Should().Be("10.0.103");
            installations[0].Subcomponents.Should().HaveCount(2);

            // Adding duplicate should not create a second entry
            manifest.AddInstallation(installRoot, installation);
            manifest.GetInstallations(installRoot).Should().ContainSingle();

            manifest.RemoveInstallation(installRoot, installation);
            manifest.GetInstallations(installRoot).Should().BeEmpty();
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }

    [Fact]
    public void AddInstalledVersionBackwardCompat()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

            var install = new DotnetInstall(installRoot, new ReleaseVersion("9.0.103"), InstallComponent.SDK);
            manifest.AddInstalledVersion(install);

            var installs = manifest.GetInstalledVersions().ToList();
            installs.Should().ContainSingle();
            installs[0].Version.ToString().Should().Be("9.0.103");
            installs[0].Component.Should().Be(InstallComponent.SDK);

            // Should also be visible through the new API
            var installations = manifest.GetInstallations(installRoot).ToList();
            installations.Should().ContainSingle();
            installations[0].Version.Should().Be("9.0.103");

            manifest.RemoveInstalledVersion(install);
            manifest.GetInstalledVersions().Should().BeEmpty();
            manifest.GetInstallations(installRoot).Should().BeEmpty();
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }

    [Fact]
    public void MultipleDotnetRoots()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);

            var root1 = new DotnetInstallRoot(@"C:\dotnet-x64", InstallArchitecture.x64);
            var root2 = new DotnetInstallRoot(@"C:\dotnet-arm64", InstallArchitecture.arm64);

            manifest.AddInstallation(root1, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });
            manifest.AddInstallation(root2, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });

            manifest.GetInstallations(root1).Should().ContainSingle();
            manifest.GetInstallations(root2).Should().ContainSingle();

            // Total should be 2 via legacy API
            manifest.GetInstalledVersions().Should().HaveCount(2);

            // Remove from one root shouldn't affect the other
            manifest.RemoveInstallation(root1, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });
            manifest.GetInstallations(root1).Should().BeEmpty();
            manifest.GetInstallations(root2).Should().ContainSingle();
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }

    [Fact]
    public void GlobalJsonInstallSpec()
    {
        var manifestPath = CreateTempManifestPath();
        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest(manifestPath);
            var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

            var spec = new InstallSpec
            {
                Component = InstallComponent.SDK,
                VersionOrChannel = "10.0.100",
                InstallSource = InstallSource.GlobalJson,
                GlobalJsonPath = @"C:\Projects\myapp\global.json"
            };

            manifest.AddInstallSpec(installRoot, spec);

            var specs = manifest.GetInstallSpecs(installRoot).ToList();
            specs.Should().ContainSingle();
            specs[0].InstallSource.Should().Be(InstallSource.GlobalJson);
            specs[0].GlobalJsonPath.Should().Be(@"C:\Projects\myapp\global.json");
        }
        finally
        {
            CleanupManifest(manifestPath);
        }
    }
}
