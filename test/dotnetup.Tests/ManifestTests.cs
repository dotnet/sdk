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
    [Fact]
    public void NewManifestCreatesValidJson()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        var json = File.ReadAllText(testEnv.ManifestPath);
        var data = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.DotnetupManifestData);

        data.Should().NotBeNull();
        data!.SchemaVersion.Should().Be("1");
        data.DotnetRoots.Should().BeEmpty();
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
    public void LegacyFormatThrowsError()
    {
        using var testEnv = new TestEnvironment();

        // Write old-format manifest (JSON array)
        File.WriteAllText(testEnv.ManifestPath, "[{\"installRoot\":{\"path\":\"C:\\\\dotnet\",\"architecture\":\"x64\"},\"version\":\"9.0.103\",\"component\":\"SDK\"}]");

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Should throw because legacy format is no longer supported
        var act = () => manifest.ReadManifest();
        act.Should().Throw<DotnetInstallException>()
            .Where(e => e.ErrorCode == DotnetInstallErrorCode.LocalManifestCorrupted)
            .WithMessage("*legacy format*no longer supported*");
    }

    [Fact]
    public void AddAndRemoveInstallSpec()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
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

    [Fact]
    public void AddAndRemoveInstallation()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
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

    [Fact]
    public void MultipleDotnetRoots()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        var root1 = new DotnetInstallRoot(@"C:\dotnet-x64", InstallArchitecture.x64);
        var root2 = new DotnetInstallRoot(@"C:\dotnet-arm64", InstallArchitecture.arm64);

        manifest.AddInstallation(root1, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });
        manifest.AddInstallation(root2, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });

        manifest.GetInstallations(root1).Should().ContainSingle();
        manifest.GetInstallations(root2).Should().ContainSingle();

        // Remove from one root shouldn't affect the other
        manifest.RemoveInstallation(root1, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });
        manifest.GetInstallations(root1).Should().BeEmpty();
        manifest.GetInstallations(root2).Should().ContainSingle();
    }

    [Fact]
    public void GlobalJsonInstallSpec()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
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

    [Fact]
    public void AddInstallSpec_DifferentSource_CreatesDuplicate()
    {
        // If AddInstallSpec is called with a different InstallSource for the same
        // component/channel, it creates a duplicate entry.
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

        // Original spec from GlobalJson
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "9.0",
            InstallSource = InstallSource.GlobalJson,
            GlobalJsonPath = @"C:\Projects\myapp\global.json"
        });

        // Additional explicit spec for the same component/channel
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "9.0",
            InstallSource = InstallSource.Explicit
        });

        // There should now be 2 specs for the same component/channel but different sources
        manifest.GetInstallSpecs(installRoot).Should().HaveCount(2);
    }

    [Fact]
    public void Update_RecordsInstallation_WithoutDuplicatingSpec()
    {
        // Simulates the fixed update flow: the update adds a new installation
        // for the channel but does NOT call AddInstallSpec (SkipInstallSpecRecording = true),
        // so no duplicate spec is created.
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

        // Initial state: GlobalJson spec + installation at 9.0.100
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "9.0",
            InstallSource = InstallSource.GlobalJson,
            GlobalJsonPath = @"C:\Projects\myapp\global.json"
        });
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "9.0.100",
            Subcomponents = ["sdk/9.0.100"]
        });

        // Simulate update: only add the new installation (no AddInstallSpec call)
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "9.0.200",
            Subcomponents = ["sdk/9.0.200"]
        });

        // Should still have exactly 1 install spec (the original GlobalJson one)
        var specs = manifest.GetInstallSpecs(installRoot).ToList();
        specs.Should().ContainSingle();
        specs[0].InstallSource.Should().Be(InstallSource.GlobalJson);

        // Should have both installations recorded
        manifest.GetInstallations(installRoot).Should().HaveCount(2);
    }

    [Fact]
    public void Update_ExplicitSpec_RecordsInstallation_WithoutDuplicatingSpec()
    {
        // Same as above but with an Explicit spec instead of GlobalJson
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(@"C:\dotnet", InstallArchitecture.x64);

        // Initial state: Explicit spec + installation at 9.0.100
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "9.0",
            InstallSource = InstallSource.Explicit
        });
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "9.0.100",
            Subcomponents = ["sdk/9.0.100"]
        });

        // Simulate update: only add the new installation (SkipInstallSpecRecording)
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "9.0.200",
            Subcomponents = ["sdk/9.0.200"]
        });

        // Should still have exactly 1 install spec
        manifest.GetInstallSpecs(installRoot).Should().ContainSingle();

        // Should have both installations
        manifest.GetInstallations(installRoot).Should().HaveCount(2);
    }
}
