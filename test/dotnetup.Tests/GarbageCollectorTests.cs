// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class GarbageCollectorTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RemovesUnreferencedInstallationRecords(bool migrated)
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
            InstallSource = migrated ? InstallSource.Migration : InstallSource.Explicit
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

    [TestMethod]
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

    [TestMethod]
    public void KeepsStableAndPreviewInstallationsWhenLatestAndPreviewSpecsExist()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);

        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "latest",
            InstallSource = InstallSource.Explicit
        });
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "preview",
            InstallSource = InstallSource.Explicit
        });

        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "10.0.100",
            Subcomponents = ["sdk/10.0.100"]
        });
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = InstallComponent.SDK,
            Version = "11.0.100-preview.1",
            Subcomponents = ["sdk/11.0.100-preview.1"]
        });

        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "10.0.100"));
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk", "11.0.100-preview.1"));

        var gc = new GarbageCollector(manifest);
        var deleted = gc.Collect(installRoot);

        deleted.Should().BeEmpty();

        manifest.GetInstallations(installRoot)
            .Select(i => i.Version)
            .Should()
            .BeEquivalentTo(["10.0.100", "11.0.100-preview.1"]);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    [DataRow(null, true, "10.0.105")]
    [DataRow("disable", true, "10.0.103")]
    [DataRow("patch", true, "10.0.105")]
    [DataRow("feature", true, "10.0.202")]
    [DataRow("minor", true, "10.1.102")]
    [DataRow("major", true, "12.0.100-preview.1")]
    [DataRow("latestPatch", true, "10.0.105")]
    [DataRow("latestFeature", true, "10.0.202")]
    [DataRow("latestMinor", true, "10.1.102")]
    [DataRow("latestMajor", true, "12.0.100-preview.1")]
    [DataRow("latestMajor", false, "11.0.101")]
    public void KeepsTheSameRepositorySdkAsList(string? policy, bool allowPrerelease, string expected)
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);
        var path = Path.Combine(testEnv.TempRoot, "global.json");
        var policyProperty = policy is null ? "" : $""","rollForward":"{policy}" """;
        File.WriteAllText(path, $$$"""
            {"sdk":{"version":"10.0.103","allowPrerelease":{{{allowPrerelease.ToString().ToLowerInvariant()}}}{{{policyProperty}}}}}
            """);
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = InstallComponent.SDK,
            VersionOrChannel = "9.0.1xx",
            InstallSource = InstallSource.GlobalJson,
            GlobalJsonPath = path
        });

        foreach (var version in new[] { "10.0.103", "10.0.105", "10.0.202", "10.1.102", "11.0.101", "12.0.100-preview.1" })
        {
            testEnv.StubComponentDirectories(null, (InstallComponent.SDK, version));
            manifest.AddInstallation(root, new Installation
            {
                Component = InstallComponent.SDK, Version = version, Subcomponents = [$"sdk/{version}"]
            });
        }

        var data = InstallationLister.GetListData(verify: false, manifestPath: testEnv.ManifestPath);
        var row = InstallationListRenderer.CreateRows(data.InstallSpecs, data.Installations).Single(r => r.Spec is not null);
        row.Installation.Should().NotBeNull();
        row.Installation!.Version.Should().Be(expected);
        manifest.GetInstallSpecs(root).Single().VersionOrChannel.Should().Be("9.0.1xx");

        new GarbageCollector(manifest).Collect(root);

        manifest.GetInstallations(root).Should().ContainSingle().Which.Version.Should().Be(expected);
        Directory.Exists(Path.Combine(testEnv.InstallPath, "sdk", expected)).Should().BeTrue();
        manifest.GetInstallSpecs(root).Should().ContainSingle().Which.VersionOrChannel.Should()
            .Be(GlobalJsonChannelResolver.CreateChannel(new GlobalJsonContents.SdkSection
            {
                Version = "10.0.103", RollForward = policy
            }).Name);
    }

    [TestMethod]
    [DataRow("missing", false, false)]
    [DataRow("versionless", false, false)]
    [DataRow("malformed", true, true)]
    [DataRow("unreadable", true, true)]
    [DataRow("unmatched", true, false)]
    public void UnresolvedRepositoryRequirementsDoNotRetainCachedInstallations(string state, bool keepSpec, bool hasError)
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);
        var path = Path.Combine(testEnv.TempRoot, "global.json");
        if (state != "missing")
        {
            File.WriteAllText(path, state switch
            {
                "versionless" => "{}",
                "malformed" => "{",
                _ => """{"sdk":{"version":"11.0.100","rollForward":"disable"}}"""
            });
        }
        using var locked = state == "unreadable" ? File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "10.0.1xx",
            InstallSource = InstallSource.GlobalJson, GlobalJsonPath = path
        });
        testEnv.StubComponentDirectories(null, (InstallComponent.SDK, "10.0.105"));
        manifest.AddInstallation(root, new Installation
        {
            Component = InstallComponent.SDK, Version = "10.0.105", Subcomponents = ["sdk/10.0.105"]
        });

        var originalError = Console.Error;
        using var error = new StringWriter();
        List<string> deleted;
        try
        {
            Console.SetError(error);
            deleted = new GarbageCollector(manifest).Collect(root);
        }
        finally
        {
            Console.SetError(originalError);
        }

        manifest.GetInstallSpecs(root).Should().HaveCount(keepSpec ? 1 : 0);
        manifest.GetInstallations(root).Should().BeEmpty();
        deleted.Should().Contain("sdk/10.0.105");
        Directory.Exists(Path.Combine(testEnv.InstallPath, "sdk", "10.0.105")).Should().BeFalse();
        if (hasError)
        {
            error.ToString().Should().Contain(path).And.Contain("Warning: Cannot read");
        }
        else
        {
            error.ToString().Should().NotContain("Warning: Cannot read");
        }
    }
}
