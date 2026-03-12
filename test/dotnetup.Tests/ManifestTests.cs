// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
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

    #region Manifest Checksum Tests

    [Fact]
    public void ManifestChecksum_WrittenOnCreate()
    {
        using var testEnv = new TestEnvironment();
        _ = new DotnetupSharedManifest(testEnv.ManifestPath);

        var checksumPath = testEnv.ManifestPath + ".sha256";
        File.Exists(checksumPath).Should().BeTrue("checksum sidecar should be created with manifest");
        File.ReadAllText(checksumPath).Trim().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ManifestChecksum_UpdatedOnWrite()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var checksumPath = testEnv.ManifestPath + ".sha256";
        var checksumBefore = File.ReadAllText(checksumPath).Trim();

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.SDK, Version = "9.0.100" });
        }

        var checksumAfter = File.ReadAllText(checksumPath).Trim();
        checksumAfter.Should().NotBe(checksumBefore, "checksum should change after add");
    }

    [Fact]
    public void ManifestCorrupted_WithValidChecksum_ThrowsLocalManifestCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Write valid manifest, then corrupt the content without updating the checksum.
        // This simulates a scenario where dotnetup wrote the file but it got corrupted
        // (e.g., disk error, partial write).
        var checksumPath = testEnv.ManifestPath + ".sha256";

        // Write corrupt JSON that still matches the stored checksum (impossible naturally,
        // so instead: write corrupt JSON, then rewrite checksum of the corrupt content)
        var corruptContent = "NOT VALID JSON {{{";
        File.WriteAllText(testEnv.ManifestPath, corruptContent);

        // Compute and write checksum of corrupt content to simulate dotnetup having written it
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(corruptContent));
        File.WriteAllText(checksumPath, Convert.ToHexString(hash));

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestCorrupted,
            "checksum matches corrupt content → product error");
    }

    [Fact]
    public void ManifestCorrupted_WithMismatchedChecksum_ThrowsLocalManifestUserCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Manifest was written by dotnetup (checksum exists), then user edits the file
        File.WriteAllText(testEnv.ManifestPath, "user broke this {[}");

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted,
            "checksum doesn't match user-edited content → user error");
    }

    [Fact]
    public void ManifestCorrupted_WithNoChecksum_ThrowsLocalManifestUserCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Delete checksum file and corrupt manifest — simulates user-created manifest
        var checksumPath = testEnv.ManifestPath + ".sha256";
        File.Delete(checksumPath);
        File.WriteAllText(testEnv.ManifestPath, "garbage data");

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted,
            "no checksum file → assume external edit → user error");
    }

    [Fact]
    public void ManifestValidation_InvalidVersion_WithChecksumMismatch_ThrowsUserCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Write a valid manifest first
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" });
        }

        // Now hand-edit the manifest with an invalid version (don't update checksum)
        var json = File.ReadAllText(testEnv.ManifestPath);
        json = json.Replace("9.0.0", "not-a-version");
        File.WriteAllText(testEnv.ManifestPath, json);

        using var mutex2 = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted,
            "checksum doesn't match user-edited content with invalid version → user error");
        ex.Message.Should().Contain("not-a-version");
    }

    [Fact]
    public void ManifestValidation_InvalidVersion_WithMatchingChecksum_SkipsValidation()
    {
        using var testEnv = new TestEnvironment();
        var checksumPath = testEnv.ManifestPath + ".sha256";

        // Write manifest with invalid version and compute matching checksum.
        // When the checksum matches, we trust the data (we wrote it) and skip validation.
        var badManifest = new DotnetupManifestData
        {
            DotnetRoots =
            [
                new DotnetRootEntry
                {
                    Path = testEnv.InstallPath,
                    Architecture = InstallerUtilities.GetDefaultInstallArchitecture(),
                    Installations =
                    [
                        new Installation { Component = InstallComponent.SDK, Version = "totally-invalid" }
                    ]
                }
            ]
        };
        var json = JsonSerializer.Serialize(badManifest, DotnetupManifestJsonContext.Default.DotnetupManifestData);
        File.WriteAllText(testEnv.ManifestPath, json);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        File.WriteAllText(checksumPath, Convert.ToHexString(hash));

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        // Should NOT throw — checksum matches so validation is skipped
        var result = manifest.ReadManifest();
        result.DotnetRoots.Should().HaveCount(1);
        result.DotnetRoots[0].Installations.Should().HaveCount(1);
    }

    [Fact]
    public void ManifestValidation_EmptyVersion_ThrowsCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var checksumPath = testEnv.ManifestPath + ".sha256";

        var badManifest = new DotnetupManifestData
        {
            DotnetRoots =
            [
                new DotnetRootEntry
                {
                    Path = testEnv.InstallPath,
                    Architecture = InstallerUtilities.GetDefaultInstallArchitecture(),
                    Installations =
                    [
                        new Installation { Component = InstallComponent.Runtime, Version = "" }
                    ]
                }
            ]
        };
        var json = JsonSerializer.Serialize(badManifest, DotnetupManifestJsonContext.Default.DotnetupManifestData);
        File.WriteAllText(testEnv.ManifestPath, json);
        // Don't write a matching checksum — this simulates user edit
        File.WriteAllText(checksumPath, "wronghash");

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted);
        ex.Message.Should().Contain("empty version");
    }

    [Fact]
    public void ManifestValidation_ValidData_DoesNotThrow()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" });
        }

        // Re-read should succeed without throwing
        DotnetupManifestData result;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            result = manifest.ReadManifest();
        }

        result.DotnetRoots.Should().HaveCount(1);
        result.DotnetRoots[0].Installations.Should().HaveCount(2);
    }

    [Fact]
    public void ManifestValidation_InvalidComponentType_ThrowsUserCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var checksumPath = testEnv.ManifestPath + ".sha256";

        // Write a manifest with an out-of-range component integer (simulates user hand-edit).
        // With UseStringEnumConverter, an undefined integer like 999 still deserializes silently.
        var json = """
            {
              "dotnetRoots": [
                {
                  "path": "PLACEHOLDER",
                  "architecture": "x86",
                  "installations": [
                    { "component": 999, "version": "9.0.0" }
                  ],
                  "installSpecs": []
                }
              ]
            }
            """.Replace("PLACEHOLDER", testEnv.InstallPath.Replace("\\", "\\\\"));
        File.WriteAllText(testEnv.ManifestPath, json);
        // Write a non-matching checksum to trigger validation
        File.WriteAllText(checksumPath, "wronghash");

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted);
        ex.Message.Should().Contain("Unknown component type");
    }

    [Fact]
    public void ManifestValidation_InvalidComponentString_ThrowsUserCorrupted()
    {
        using var testEnv = new TestEnvironment();
        var checksumPath = testEnv.ManifestPath + ".sha256";

        // Write a manifest with an unrecognized string component (simulates user hand-edit).
        // System.Text.Json will throw a JsonException for an unknown string enum value.
        var json = """
            {
              "dotnetRoots": [
                {
                  "path": "PLACEHOLDER",
                  "architecture": "x86",
                  "installations": [
                    { "component": "FooBar", "version": "9.0.0" }
                  ],
                  "installSpecs": []
                }
              ]
            }
            """.Replace("PLACEHOLDER", testEnv.InstallPath.Replace("\\", "\\\\"));
        File.WriteAllText(testEnv.ManifestPath, json);
        File.WriteAllText(checksumPath, "wronghash");

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.ReadManifest());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted,
            "invalid string enum value should fail JSON deserialization and be detected as user edit");
    }

    #endregion

    #region Manifest Tracking Tests

    [Fact]
    public void ManifestTracking_DifferentComponents_TrackedSeparately()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Add SDK, Runtime, ASPNETCore
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.SDK, Version = "9.0.100" });
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" });
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.ASPNETCore, Version = "9.0.0" });
        }

        IEnumerable<Installation> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstallations(installRoot).ToList();
        }

        installs.Should().HaveCount(3);
        installs.Should().Contain(i => i.Component == InstallComponent.SDK);
        installs.Should().Contain(i => i.Component == InstallComponent.Runtime);
        installs.Should().Contain(i => i.Component == InstallComponent.ASPNETCore);
    }

    [Fact]
    public void ManifestTracking_SameVersionDifferentComponent_BothTracked()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" });
            manifest.AddInstallation(installRoot, new Installation { Component = InstallComponent.ASPNETCore, Version = "9.0.0" });
        }

        IEnumerable<Installation> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstallations(installRoot).ToList();
        }

        installs.Should().HaveCount(2);
    }

    #endregion
}
